using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.Models;

namespace Mostlylucid.RagLlmSearch.Conversation;

/// <summary>
/// SQLite-based conversation history service
/// </summary>
public class SqliteConversationService : IConversationService, IAsyncDisposable
{
    private readonly RagLlmSearchOptions _options;
    private readonly ILogger<SqliteConversationService> _logger;
    private SqliteConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public SqliteConversationService(
        IOptions<RagLlmSearchOptions> options,
        ILogger<SqliteConversationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            var connectionString = $"Data Source={_options.DatabasePath}";
            _connection = new SqliteConnection(connectionString);
            await _connection.OpenAsync(cancellationToken);

            var createTablesSql = """
                CREATE TABLE IF NOT EXISTS conversations (
                    id TEXT PRIMARY KEY,
                    user_id TEXT,
                    title TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    is_active INTEGER DEFAULT 1,
                    metadata TEXT
                );

                CREATE TABLE IF NOT EXISTS messages (
                    id TEXT PRIMARY KEY,
                    conversation_id TEXT NOT NULL,
                    role TEXT NOT NULL,
                    content TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    sources TEXT,
                    token_count INTEGER,
                    used_rag_context INTEGER DEFAULT 0,
                    triggered_search INTEGER DEFAULT 0,
                    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_conversations_user ON conversations(user_id);
                CREATE INDEX IF NOT EXISTS idx_messages_conversation ON messages(conversation_id);
                CREATE INDEX IF NOT EXISTS idx_messages_created ON messages(created_at);
                """;

            await using var command = new SqliteCommand(createTablesSql, _connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
            _logger.LogInformation("Conversation service initialized with database: {Path}", _options.DatabasePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Models.Conversation> CreateConversationAsync(
        string? userId = null,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var conversation = new Models.Conversation
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Title = title ?? "New Conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sql = """
                INSERT INTO conversations (id, user_id, title, created_at, updated_at, is_active, metadata)
                VALUES (@id, @user_id, @title, @created_at, @updated_at, @is_active, @metadata)
                """;

            await using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@id", conversation.Id);
            command.Parameters.AddWithValue("@user_id", userId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@title", conversation.Title);
            command.Parameters.AddWithValue("@created_at", conversation.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("@updated_at", conversation.UpdatedAt.ToString("O"));
            command.Parameters.AddWithValue("@is_active", 1);
            command.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(conversation.Metadata));

            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogDebug("Created conversation {Id}", conversation.Id);

            return conversation;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Models.Conversation?> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sql = "SELECT * FROM conversations WHERE id = @id";
            await using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@id", conversationId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var conversation = ReadConversation(reader);
                // Load messages
                conversation.Messages = await GetMessagesInternalAsync(conversationId, null, cancellationToken);
                return conversation;
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Models.Conversation>> GetUserConversationsAsync(
        string userId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sql = includeInactive
                ? "SELECT * FROM conversations WHERE user_id = @user_id ORDER BY updated_at DESC"
                : "SELECT * FROM conversations WHERE user_id = @user_id AND is_active = 1 ORDER BY updated_at DESC";

            await using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@user_id", userId);

            var conversations = new List<Models.Conversation>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                conversations.Add(ReadConversation(reader));
            }

            return conversations;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChatMessage> AddMessageAsync(
        string conversationId,
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        message.ConversationId = conversationId;
        if (string.IsNullOrEmpty(message.Id))
        {
            message.Id = Guid.NewGuid().ToString();
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sql = """
                INSERT INTO messages (id, conversation_id, role, content, created_at, sources, token_count, used_rag_context, triggered_search)
                VALUES (@id, @conversation_id, @role, @content, @created_at, @sources, @token_count, @used_rag_context, @triggered_search)
                """;

            await using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@id", message.Id);
            command.Parameters.AddWithValue("@conversation_id", conversationId);
            command.Parameters.AddWithValue("@role", message.Role.ToString());
            command.Parameters.AddWithValue("@content", message.Content);
            command.Parameters.AddWithValue("@created_at", message.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("@sources", JsonSerializer.Serialize(message.Sources));
            command.Parameters.AddWithValue("@token_count", message.TokenCount ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@used_rag_context", message.UsedRagContext ? 1 : 0);
            command.Parameters.AddWithValue("@triggered_search", message.TriggeredSearch ? 1 : 0);

            await command.ExecuteNonQueryAsync(cancellationToken);

            // Update conversation timestamp
            var updateSql = "UPDATE conversations SET updated_at = @updated_at WHERE id = @id";
            await using var updateCommand = new SqliteCommand(updateSql, _connection);
            updateCommand.Parameters.AddWithValue("@id", conversationId);
            updateCommand.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("O"));
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Added message {Id} to conversation {ConversationId}", message.Id, conversationId);
            return message;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(
        string conversationId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await GetMessagesInternalAsync(conversationId, limit, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ChatMessage>> GetMessagesInternalAsync(
        string conversationId,
        int? limit,
        CancellationToken cancellationToken)
    {
        var sql = limit.HasValue
            ? "SELECT * FROM messages WHERE conversation_id = @conversation_id ORDER BY created_at ASC LIMIT @limit"
            : "SELECT * FROM messages WHERE conversation_id = @conversation_id ORDER BY created_at ASC";

        await using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@conversation_id", conversationId);
        if (limit.HasValue)
        {
            command.Parameters.AddWithValue("@limit", limit.Value);
        }

        var messages = new List<ChatMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadMessage(reader));
        }

        return messages;
    }

    public async Task UpdateConversationAsync(
        string conversationId,
        string? title = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var updates = new List<string> { "updated_at = @updated_at" };
            var parameters = new List<SqliteParameter>
            {
                new("@id", conversationId),
                new("@updated_at", DateTime.UtcNow.ToString("O"))
            };

            if (title != null)
            {
                updates.Add("title = @title");
                parameters.Add(new SqliteParameter("@title", title));
            }

            if (isActive.HasValue)
            {
                updates.Add("is_active = @is_active");
                parameters.Add(new SqliteParameter("@is_active", isActive.Value ? 1 : 0));
            }

            var sql = $"UPDATE conversations SET {string.Join(", ", updates)} WHERE id = @id";
            await using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Delete messages first
            var deleteMessagesSql = "DELETE FROM messages WHERE conversation_id = @id";
            await using var deleteMessagesCommand = new SqliteCommand(deleteMessagesSql, _connection);
            deleteMessagesCommand.Parameters.AddWithValue("@id", conversationId);
            await deleteMessagesCommand.ExecuteNonQueryAsync(cancellationToken);

            // Delete conversation
            var sql = "DELETE FROM conversations WHERE id = @id";
            await using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("@id", conversationId);
            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Deleted conversation {Id}", conversationId);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    private static Models.Conversation ReadConversation(SqliteDataReader reader)
    {
        var metadataJson = reader["metadata"] as string ?? "{}";

        return new Models.Conversation
        {
            Id = reader["id"].ToString() ?? string.Empty,
            UserId = reader["user_id"] as string,
            Title = reader["title"].ToString() ?? "New Conversation",
            CreatedAt = DateTime.Parse(reader["created_at"].ToString() ?? DateTime.UtcNow.ToString("O")),
            UpdatedAt = DateTime.Parse(reader["updated_at"].ToString() ?? DateTime.UtcNow.ToString("O")),
            IsActive = Convert.ToInt32(reader["is_active"]) == 1,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson) ?? new()
        };
    }

    private static ChatMessage ReadMessage(SqliteDataReader reader)
    {
        var sourcesJson = reader["sources"] as string ?? "[]";
        var roleString = reader["role"].ToString() ?? "User";

        return new ChatMessage
        {
            Id = reader["id"].ToString() ?? string.Empty,
            ConversationId = reader["conversation_id"].ToString() ?? string.Empty,
            Role = Enum.TryParse<ChatRole>(roleString, true, out var role) ? role : ChatRole.User,
            Content = reader["content"].ToString() ?? string.Empty,
            CreatedAt = DateTime.Parse(reader["created_at"].ToString() ?? DateTime.UtcNow.ToString("O")),
            Sources = JsonSerializer.Deserialize<List<SourceReference>>(sourcesJson) ?? new(),
            TokenCount = reader["token_count"] as int?,
            UsedRagContext = Convert.ToInt32(reader["used_rag_context"]) == 1,
            TriggeredSearch = Convert.ToInt32(reader["triggered_search"]) == 1
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        _lock.Dispose();
    }
}
