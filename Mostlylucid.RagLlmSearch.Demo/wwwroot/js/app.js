// RAG LLM Search Demo App
class ChatApp {
    constructor() {
        this.connection = null;
        this.currentConversationId = null;
        this.userId = this.getOrCreateUserId();
        this.isStreaming = false;
        this.currentMessageElement = null;

        this.initializeElements();
        this.initializeSignalR();
        this.loadProviders();
        this.setupEventListeners();
    }

    getOrCreateUserId() {
        let userId = localStorage.getItem('userId');
        if (!userId) {
            userId = 'user_' + Math.random().toString(36).substr(2, 9);
            localStorage.setItem('userId', userId);
        }
        return userId;
    }

    initializeElements() {
        this.chatMessages = document.getElementById('chatMessages');
        this.messageInput = document.getElementById('messageInput');
        this.chatForm = document.getElementById('chatForm');
        this.sendButton = document.getElementById('sendButton');
        this.statusElement = document.getElementById('status');
        this.statusText = document.querySelector('.status-text');
        this.searchProviderSelect = document.getElementById('searchProvider');
        this.enableWebSearch = document.getElementById('enableWebSearch');
        this.enableRag = document.getElementById('enableRag');
        this.conversationList = document.getElementById('conversationList');
        this.newConversationBtn = document.getElementById('newConversation');
        this.ragContent = document.getElementById('ragContent');
        this.ragTitle = document.getElementById('ragTitle');
        this.ragUrl = document.getElementById('ragUrl');
        this.addRagBtn = document.getElementById('addRag');
    }

    async initializeSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('/chathub')
            .withAutomaticReconnect()
            .build();

        this.connection.on('ReceiveChunk', (chunk) => this.handleChunk(chunk));
        this.connection.on('ReceiveError', (error) => this.handleError(error));
        this.connection.on('ConversationDeleted', (id) => this.handleConversationDeleted(id));

        this.connection.onreconnecting(() => this.updateStatus('reconnecting'));
        this.connection.onreconnected(() => this.updateStatus('connected'));
        this.connection.onclose(() => this.updateStatus('disconnected'));

        try {
            await this.connection.start();
            this.updateStatus('connected');
            await this.loadConversations();
        } catch (err) {
            console.error('SignalR connection error:', err);
            this.updateStatus('disconnected');
        }
    }

    updateStatus(status) {
        this.statusElement.className = 'status ' + status;
        const statusTexts = {
            'connected': 'Connected',
            'disconnected': 'Disconnected',
            'reconnecting': 'Reconnecting...'
        };
        this.statusText.textContent = statusTexts[status] || status;
    }

    async loadProviders() {
        try {
            const response = await fetch('/api/chat/providers');
            const providers = await response.json();

            providers.forEach(provider => {
                const option = document.createElement('option');
                option.value = provider;
                option.textContent = provider;
                this.searchProviderSelect.appendChild(option);
            });
        } catch (err) {
            console.error('Error loading providers:', err);
        }
    }

    setupEventListeners() {
        this.chatForm.addEventListener('submit', (e) => this.handleSubmit(e));

        this.messageInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.chatForm.dispatchEvent(new Event('submit'));
            }
        });

        this.messageInput.addEventListener('input', () => this.autoResizeTextarea());

        this.newConversationBtn.addEventListener('click', () => this.startNewConversation());
        this.addRagBtn.addEventListener('click', () => this.addRagDocument());
    }

    autoResizeTextarea() {
        this.messageInput.style.height = 'auto';
        this.messageInput.style.height = Math.min(this.messageInput.scrollHeight, 150) + 'px';
    }

    async handleSubmit(e) {
        e.preventDefault();

        const message = this.messageInput.value.trim();
        if (!message || this.isStreaming) return;

        this.clearWelcomeMessage();
        this.addMessage('user', message);
        this.messageInput.value = '';
        this.autoResizeTextarea();

        const request = {
            conversationId: this.currentConversationId,
            message: message,
            userId: this.userId,
            enableWebSearch: this.enableWebSearch.checked,
            enableRag: this.enableRag.checked,
            searchProvider: this.searchProviderSelect.value || null,
            stream: true
        };

        this.isStreaming = true;
        this.sendButton.disabled = true;
        this.currentMessageElement = this.addMessage('assistant', '', true);

        try {
            await this.connection.invoke('SendMessage', request);
        } catch (err) {
            this.handleError(err.message);
        }
    }

    handleChunk(chunk) {
        if (!this.currentConversationId && chunk.conversationId) {
            this.currentConversationId = chunk.conversationId;
            this.loadConversations();
        }

        if (chunk.content) {
            const textElement = this.currentMessageElement.querySelector('.message-text');
            textElement.textContent += chunk.content;
            this.scrollToBottom();
        }

        if (chunk.isFinal) {
            this.isStreaming = false;
            this.sendButton.disabled = false;

            // Remove typing indicator
            const typing = this.currentMessageElement.querySelector('.typing-indicator');
            if (typing) typing.remove();

            // Add sources if available
            if (chunk.sources && chunk.sources.length > 0) {
                this.addSourcesToMessage(this.currentMessageElement, chunk.sources);
            }

            this.currentMessageElement = null;
        }
    }

    handleError(error) {
        this.isStreaming = false;
        this.sendButton.disabled = false;

        if (this.currentMessageElement) {
            const textElement = this.currentMessageElement.querySelector('.message-text');
            textElement.textContent = `Error: ${error}`;
            textElement.style.color = 'var(--error)';
        }

        this.currentMessageElement = null;
    }

    clearWelcomeMessage() {
        const welcome = this.chatMessages.querySelector('.welcome-message');
        if (welcome) welcome.remove();
    }

    addMessage(role, content, isStreaming = false) {
        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${role}`;

        const avatar = document.createElement('div');
        avatar.className = 'message-avatar';
        avatar.textContent = role === 'user' ? 'U' : 'AI';

        const contentDiv = document.createElement('div');
        contentDiv.className = 'message-content';

        const textDiv = document.createElement('div');
        textDiv.className = 'message-text';
        textDiv.textContent = content;

        contentDiv.appendChild(textDiv);

        if (isStreaming) {
            const typing = document.createElement('div');
            typing.className = 'typing-indicator';
            typing.innerHTML = '<span></span><span></span><span></span>';
            contentDiv.appendChild(typing);
        }

        messageDiv.appendChild(avatar);
        messageDiv.appendChild(contentDiv);

        this.chatMessages.appendChild(messageDiv);
        this.scrollToBottom();

        return messageDiv;
    }

    addSourcesToMessage(messageElement, sources) {
        const contentDiv = messageElement.querySelector('.message-content');

        const sourcesDiv = document.createElement('div');
        sourcesDiv.className = 'message-sources';

        const title = document.createElement('div');
        title.className = 'message-sources-title';
        title.textContent = 'Sources:';
        sourcesDiv.appendChild(title);

        sources.forEach(source => {
            if (source.url) {
                const link = document.createElement('a');
                link.className = 'source-link';
                link.href = source.url;
                link.target = '_blank';
                link.textContent = source.title || source.url;
                sourcesDiv.appendChild(link);
            }
        });

        contentDiv.appendChild(sourcesDiv);
    }

    scrollToBottom() {
        this.chatMessages.scrollTop = this.chatMessages.scrollHeight;
    }

    async loadConversations() {
        try {
            const conversations = await this.connection.invoke('GetUserConversations', this.userId);
            this.renderConversations(conversations);
        } catch (err) {
            console.error('Error loading conversations:', err);
        }
    }

    renderConversations(conversations) {
        this.conversationList.innerHTML = '';

        conversations.forEach(conv => {
            const item = document.createElement('div');
            item.className = 'conversation-item' + (conv.id === this.currentConversationId ? ' active' : '');
            item.innerHTML = `
                <div class="conversation-item-title">${conv.title}</div>
                <div class="conversation-item-date">${new Date(conv.updatedAt).toLocaleDateString()}</div>
            `;
            item.addEventListener('click', () => this.loadConversation(conv.id));
            this.conversationList.appendChild(item);
        });
    }

    async loadConversation(conversationId) {
        try {
            const conversation = await this.connection.invoke('GetConversation', conversationId);
            if (conversation) {
                this.currentConversationId = conversationId;
                this.clearChat();

                conversation.messages.forEach(msg => {
                    const role = msg.role.toLowerCase() === 'user' ? 'user' : 'assistant';
                    const element = this.addMessage(role, msg.content);

                    if (msg.sources && msg.sources.length > 0) {
                        this.addSourcesToMessage(element, msg.sources);
                    }
                });

                this.loadConversations();
            }
        } catch (err) {
            console.error('Error loading conversation:', err);
        }
    }

    startNewConversation() {
        this.currentConversationId = null;
        this.clearChat();
        this.loadConversations();
    }

    clearChat() {
        this.chatMessages.innerHTML = '';
    }

    handleConversationDeleted(conversationId) {
        if (this.currentConversationId === conversationId) {
            this.startNewConversation();
        }
        this.loadConversations();
    }

    async addRagDocument() {
        const content = this.ragContent.value.trim();
        if (!content) {
            alert('Please enter content to add');
            return;
        }

        try {
            const response = await fetch('/api/chat/rag', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    title: this.ragTitle.value.trim() || 'User Input',
                    content: content,
                    sourceUrl: this.ragUrl.value.trim() || null,
                    documentType: 'user_input'
                })
            });

            if (response.ok) {
                this.ragContent.value = '';
                this.ragTitle.value = '';
                this.ragUrl.value = '';
                alert('Document added to knowledge base!');
            } else {
                alert('Error adding document');
            }
        } catch (err) {
            console.error('Error adding RAG document:', err);
            alert('Error adding document');
        }
    }
}

// Initialize app when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.chatApp = new ChatApp();
});
