using Mostlylucid.RagLlmSearch.Models;

namespace Mostlylucid.RagLlmSearch.Rag;

/// <summary>
///     Interface for RAG (Retrieval Augmented Generation) service
/// </summary>
public interface IRagService
{
    /// <summary>
    ///     Adds a document to the RAG store
    /// </summary>
    /// <param name="document">Document to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddDocumentAsync(RagDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds multiple documents to the RAG store
    /// </summary>
    /// <param name="documents">Documents to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddDocumentsAsync(IEnumerable<RagDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Searches for similar documents based on query
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="maxResults">Maximum results to return</param>
    /// <param name="minScore">Minimum similarity score (0.0 - 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of similar documents with scores</returns>
    Task<List<RagSearchResult>> SearchAsync(
        string query,
        int maxResults = 5,
        float minScore = 0.5f,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a document by ID
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<RagDocument?> GetDocumentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a document by ID
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteDocumentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears all documents from the RAG store
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the total number of documents in the store
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> GetDocumentCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Initializes the RAG store (creates tables, etc.)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}