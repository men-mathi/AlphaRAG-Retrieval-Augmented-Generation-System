document.addEventListener('DOMContentLoaded', () => {
    // --- DOM Elements ---
    const sidebar = document.getElementById('sidebar');
    const mobileToggleBtn = document.getElementById('mobile-toggle-btn');
    const mobileCloseBtn = document.getElementById('mobile-close-btn');
    const chatFeed = document.getElementById('chat-feed');
    const welcomeCard = document.getElementById('welcome-card');
    const chatForm = document.getElementById('chat-form');
    const userInput = document.getElementById('user-input');
    const btnSend = document.getElementById('btn-send');
    const clearChatBtn = document.getElementById('clear-chat-btn');
    const ingestBtn = document.getElementById('ingest-btn');
    const ingestBtnText = document.getElementById('ingest-btn-text');
    const ingestSpinner = document.getElementById('ingest-spinner');
    const searchHistoryList = document.getElementById('search-history-list');
    const clearSearchHistoryBtn = document.getElementById('clear-search-history-btn');
    const newChatBtn = document.getElementById('new-chat-btn');
    const chatSessionsList = document.getElementById('chat-sessions-list');
    
    // Status elements
    const healthIndicator = document.getElementById('health-indicator');
    const healthText = document.getElementById('health-text');
    const llmProvider = document.getElementById('llm-provider');
    const embeddingProvider = document.getElementById('embedding-provider');
    const docsFolder = document.getElementById('docs-folder');
    
    let sessions = [];
    let currentSessionId = '';
    const SESSIONS_KEY = 'alpharag_chat_sessions';
    const CURRENT_SESSION_ID_KEY = 'alpharag_current_session_id';
 
    let searchHistory = [];
    const SEARCH_HISTORY_KEY = 'alpharag_search_history';

    // --- Initialization ---
    init();

    function init() {
        setupEventListeners();
        checkSystemHealth();
        loadSessions();
        loadSearchHistory();
        adjustTextareaHeight();
    }

    // --- Event Listeners Setup ---
    function setupEventListeners() {
        // Mobile Sidebar Toggle
        mobileToggleBtn.addEventListener('click', () => {
            sidebar.classList.add('active');
        });

        mobileCloseBtn.addEventListener('click', () => {
            sidebar.classList.remove('active');
        });

        // Close sidebar on mobile when clicking outside
        document.addEventListener('click', (e) => {
            if (window.innerWidth <= 900 && 
                !sidebar.contains(e.target) && 
                !mobileToggleBtn.contains(e.target) && 
                sidebar.classList.contains('active')) {
                sidebar.classList.remove('active');
            }
        });

        // Chat Input Handlers
        userInput.addEventListener('input', () => {
            adjustTextareaHeight();
            btnSend.disabled = !userInput.value.trim();
        });

        userInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (userInput.value.trim()) {
                    submitMessage();
                }
            }
        });

        chatForm.addEventListener('submit', (e) => {
            e.preventDefault();
            submitMessage();
        });


        // Clear Chat History
        clearChatBtn.addEventListener('click', () => {
            if (confirm('Are you sure you want to clear the chat history?')) {
                clearHistory();
            }
        });

        // Ingestion Trigger
        ingestBtn.addEventListener('click', triggerDocumentIngestion);

        // Clear Search History
        clearSearchHistoryBtn.addEventListener('click', clearSearchHistory);

        // Handle clicking on history item or its delete button (Event Delegation)
        searchHistoryList.addEventListener('click', (e) => {
            const chip = e.target.closest('.history-item-chip');
            if (!chip) return;

            const query = chip.getAttribute('data-query');

            // If clicked the delete button
            const deleteBtn = e.target.closest('.history-item-delete');
            if (deleteBtn) {
                e.stopPropagation();
                deleteSearchHistoryItem(query);
                return;
            }

            // Otherwise, populate and submit search query
            userInput.value = query;
            adjustTextareaHeight();
            btnSend.disabled = false;
            userInput.focus();
            
            // Close sidebar on mobile
            if (window.innerWidth <= 900) {
                sidebar.classList.remove('active');
            }
            
            submitMessage();
        });

        // New Chat Button Click
        newChatBtn.addEventListener('click', createNewSession);

        // Chat Sessions List Click/Delete (Event Delegation)
        chatSessionsList.addEventListener('click', (e) => {
            const chip = e.target.closest('.session-item-chip');
            if (!chip) return;

            const sessionId = chip.getAttribute('data-id');

            // Check if delete button was clicked
            const deleteBtn = e.target.closest('.session-item-delete');
            if (deleteBtn) {
                e.stopPropagation();
                deleteSession(sessionId);
                return;
            }

            // Otherwise, switch session
            if (sessionId !== currentSessionId) {
                currentSessionId = sessionId;
                saveSessions();
                renderSessionsList();
                renderActiveSessionMessages();
            }

            // Close sidebar on mobile
            if (window.innerWidth <= 900) {
                sidebar.classList.remove('active');
            }
        });
    }

    // --- Helper: Dynamic Textarea Height ---
    function adjustTextareaHeight() {
        userInput.style.height = 'auto';
        userInput.style.height = (userInput.scrollHeight) + 'px';
    }

    // --- API Service Calls ---

    // Fetch Backend Health Status
    async function checkSystemHealth() {
        setHealthState('checking');
        try {
            const response = await fetch('/health');
            if (!response.ok) throw new Error('API server returned error');
            
            const data = await response.json();
            
            // Render health details
            if (healthText) healthText.textContent = data.status || 'Healthy';
            if (llmProvider) llmProvider.textContent = data.llmProvider || '-';
            if (embeddingProvider) embeddingProvider.textContent = data.embeddingProvider || '-';
            if (docsFolder) {
                docsFolder.textContent = data.docsFolder || 'docs';
                docsFolder.title = `Folder Path: ${data.docsFolder}\nChunk Size: ${data.chunkSize}\nOverlap: ${data.chunkOverlap}`;
            }
            
            setHealthState('healthy');
        } catch (error) {
            console.error('System Health check failed:', error);
            if (healthText) healthText.textContent = 'Offline';
            if (llmProvider) llmProvider.textContent = 'N/A';
            if (embeddingProvider) embeddingProvider.textContent = 'N/A';
            setHealthState('unhealthy');
            showToast('Unable to connect to RAG API backend service.', 'error');
        }
    }

    // Set UI indicators based on health state
    function setHealthState(state) {
        if (!healthIndicator) return;
        healthIndicator.className = 'status-indicator';
        if (state === 'healthy') {
            healthIndicator.classList.add('status-healthy');
        } else if (state === 'checking') {
            healthIndicator.classList.add('status-unknown');
        } else {
            healthIndicator.classList.add('status-unhealthy');
        }
    }

    // Trigger Ingestion Endpoint
    async function triggerDocumentIngestion() {
        // Update button visual state
        ingestBtn.disabled = true;
        ingestBtnText.textContent = 'Indexing...';
        ingestSpinner.classList.remove('icon-hidden');
        
        appendSystemMessage('System triggered manual document ingestion. Re-indexing docs folder...');

        try {
            const response = await fetch('/ingest', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            const result = await response.json();
            
            if (response.ok) {
                showToast(result.message || 'Ingestion completed successfully!', 'success');
                appendSystemMessage('Document re-indexing completed successfully. Vector index updated.');
            } else {
                throw new Error(result.error || 'Ingestion endpoint failed');
            }
        } catch (error) {
            console.error('Ingestion failed:', error);
            showToast('Data Ingestion failed. See server logs for details.', 'error');
            appendSystemMessage('Document ingestion failed. Please check backend logs.');
        } finally {
            // Restore button visual state
            ingestBtn.disabled = false;
            ingestBtnText.textContent = 'Ingest Documents';
            ingestSpinner.classList.add('icon-hidden');
            checkSystemHealth();
        }
    }

    // Submit Chat Message
    async function submitMessage() {
        const text = userInput.value.trim();
        if (!text) return;

        // Add to search history
        addToSearchHistory(text);

        // Add user message to UI
        appendMessage('user', text);
        
        // Clear input field
        userInput.value = '';
        adjustTextareaHeight();
        btnSend.disabled = true;

        // Hide welcome card if visible
        if (welcomeCard) {
            welcomeCard.style.display = 'none';
        }

        // Show typing indicator
        const typingId = showTypingIndicator();

        try {
            const response = await fetch('/chat', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    message: text,
                    limit: 3
                })
            });

            if (!response.ok) {
                const errText = await response.text();
                throw new Error(errText || 'Chat API returned error code');
            }

            const data = await response.json();
            
            // Remove typing indicator
            removeTypingIndicator(typingId);

            // Add AI response to UI
            appendMessage('ai', data.answer, data.sources);
        } catch (error) {
            console.error('Chat error:', error);
            removeTypingIndicator(typingId);
            appendMessage('ai', `**Error**: Failed to generate a response from the service. Make sure the API backend is running and reachable.\n\n*Technical info:* ${error.message}`);
            showToast('Failed to contact RAG chatbot endpoint.', 'error');
        }
    }

    // --- Message Rendering & Formatting Helpers ---

    function appendMessage(role, content, sources = []) {
        const messageRow = document.createElement('div');
        messageRow.classList.add('message-row', `message-row-${role}`);

        const messageBubble = document.createElement('div');
        messageBubble.classList.add('message-bubble', `message-bubble-${role}`);
        
        // Process message content markdown
        messageBubble.innerHTML = formatMessageContent(content);

        // Append sources if present (for AI responses)
        if (role === 'ai' && sources && sources.length > 0) {
            const metaDiv = document.createElement('div');
            metaDiv.classList.add('message-meta');
            
            const sourcesContainer = document.createElement('div');
            sourcesContainer.classList.add('sources-container');
            
            const label = document.createElement('span');
            label.classList.add('sources-label');
            label.textContent = 'Sources: ';
            sourcesContainer.appendChild(label);
            
            sources.forEach(src => {
                const badge = document.createElement('span');
                badge.classList.add('source-badge');
                badge.textContent = src;
                badge.title = `Answer retrieved from ${src}`;
                sourcesContainer.appendChild(badge);
            });

            metaDiv.appendChild(sourcesContainer);
            messageBubble.appendChild(metaDiv);
        }

        messageRow.appendChild(messageBubble);
        chatFeed.appendChild(messageRow);
        
        // Save to State history
        saveToHistory({ role, content, sources });
        scrollToBottom();
    }

    function appendSystemMessage(content) {
        const messageRow = document.createElement('div');
        messageRow.classList.add('message-row', 'message-row-system');

        const messageBubble = document.createElement('div');
        messageBubble.classList.add('message-bubble-system');
        messageBubble.textContent = content;

        messageRow.appendChild(messageBubble);
        chatFeed.appendChild(messageRow);
        
        saveToHistory({ role: 'system', content });
        scrollToBottom();
    }

    // Typing Indicator
    function showTypingIndicator() {
        const id = 'typing-' + Date.now();
        const messageRow = document.createElement('div');
        messageRow.classList.add('message-row', 'message-row-ai');
        messageRow.id = id;

        const messageBubble = document.createElement('div');
        messageBubble.classList.add('message-bubble', 'message-bubble-ai');

        const typingIndicator = document.createElement('div');
        typingIndicator.classList.add('typing-indicator');
        
        for (let i = 0; i < 3; i++) {
            const dot = document.createElement('div');
            dot.classList.add('typing-dot');
            typingIndicator.appendChild(dot);
        }

        messageBubble.appendChild(typingIndicator);
        messageRow.appendChild(messageBubble);
        chatFeed.appendChild(messageRow);
        scrollToBottom();

        return id;
    }

    function removeTypingIndicator(id) {
        const element = document.getElementById(id);
        if (element) {
            element.remove();
        }
    }

    // Scroll chat feed to bottom
    function scrollToBottom() {
        chatFeed.scrollTop = chatFeed.scrollHeight;
    }

    // Simple Custom Markdown Formatter for rendering lists, code blocks, bold text
    function formatMessageContent(text) {
        if (!text) return '';
        
        let html = text;

        // Escape HTML entities to prevent XSS (except for our custom markdown conversions)
        html = html
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");

        // Code blocks: ```code```
        html = html.replace(/```([\s\S]*?)```/g, (match, code) => {
            return `<pre><code>${code.trim()}</code></pre>`;
        });

        // Inline code: `code`
        html = html.replace(/`([^`]+)`/g, '<code>$1</code>');

        // Bold text: **text**
        html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

        // Bulleted lists: lines starting with * or - followed by space
        const lines = html.split('\n');
        let inList = false;
        const processedLines = lines.map(line => {
            const trimmed = line.trim();
            if (trimmed.startsWith('* ') || trimmed.startsWith('- ')) {
                const listContent = trimmed.substring(2);
                if (!inList) {
                    inList = true;
                    return `<ul><li>${listContent}</li>`;
                }
                return `<li>${listContent}</li>`;
            } else {
                if (inList) {
                    inList = false;
                    return `</ul>${line}`;
                }
                return line;
            }
        });
        if (inList) {
            processedLines.push('</ul>');
        }
        html = processedLines.join('\n');

        // Paragraphs & Linebreaks
        html = html.split('\n\n').map(para => {
            const trimmedPara = para.trim();
            if (!trimmedPara) return '';
            
            // Check if it's already list html or code block html
            if (trimmedPara.startsWith('<pre>') || trimmedPara.startsWith('<ul>') || trimmedPara.startsWith('<li>') || trimmedPara.startsWith('</ul>')) {
                return trimmedPara;
            }
            return `<p>${trimmedPara.replace(/\n/g, '<br>')}</p>`;
        }).join('');

        return html;
    }

    // --- Local Storage & Session Management ---
    
    function loadSessions() {
        const storedSessions = localStorage.getItem(SESSIONS_KEY);
        const storedActiveId = localStorage.getItem(CURRENT_SESSION_ID_KEY);
        
        if (storedSessions) {
            try {
                sessions = JSON.parse(storedSessions);
            } catch (e) {
                console.error('Failed to parse sessions list:', e);
                sessions = [];
            }
        }
        
        if (sessions.length === 0) {
            // Create a default first session
            const defaultId = 'session_' + Date.now();
            sessions.push({
                id: defaultId,
                title: 'New Chat',
                messages: [],
                timestamp: Date.now()
            });
            currentSessionId = defaultId;
        } else {
            // Find active session
            if (storedActiveId && sessions.some(s => s.id === storedActiveId)) {
                currentSessionId = storedActiveId;
            } else {
                currentSessionId = sessions[0].id;
            }
        }
        
        saveSessions();
        renderSessionsList();
        renderActiveSessionMessages();
    }

    function saveSessions() {
        localStorage.setItem(SESSIONS_KEY, JSON.stringify(sessions));
        localStorage.setItem(CURRENT_SESSION_ID_KEY, currentSessionId);
    }

    function createNewSession() {
        const newSessionId = 'session_' + Date.now();
        const newSession = {
            id: newSessionId,
            title: 'New Chat',
            messages: [],
            timestamp: Date.now()
        };
        sessions.unshift(newSession);
        currentSessionId = newSessionId;
        saveSessions();
        renderSessionsList();
        renderActiveSessionMessages();
        showToast('Started a new conversation.', 'success', 2000);
    }

    function renderSessionsList() {
        chatSessionsList.innerHTML = '';
        sessions.forEach(session => {
            const chip = document.createElement('div');
            chip.className = `session-item-chip ${session.id === currentSessionId ? 'active' : ''}`;
            chip.setAttribute('data-id', session.id);

            const escapedTitle = session.title
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#039;");

            chip.innerHTML = `
                <span class="session-item-icon">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="width:15px;height:15px;">
                        <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
                    </svg>
                </span>
                <span class="session-item-text" title="${escapedTitle}">${escapedTitle}</span>
                <button class="session-item-delete" title="Delete conversation">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                        <line x1="18" y1="6" x2="6" y2="18"></line>
                        <line x1="6" y1="6" x2="18" y2="18"></line>
                    </svg>
                </button>
            `;
            chatSessionsList.appendChild(chip);
        });
    }

    function renderActiveSessionMessages() {
        chatFeed.innerHTML = '';
        const activeSession = sessions.find(s => s.id === currentSessionId);
        
        if (!activeSession || activeSession.messages.length === 0) {
            chatFeed.appendChild(welcomeCard);
            welcomeCard.style.display = 'block';
        } else {
            welcomeCard.style.display = 'none';
            activeSession.messages.forEach(msg => {
                if (msg.role === 'system') {
                    appendSystemMsgUI(msg.content);
                } else {
                    appendMsgUI(msg.role, msg.content, msg.sources);
                }
            });
            scrollToBottom();
        }
    }

    function deleteSession(sessionId) {
        sessions = sessions.filter(s => s.id !== sessionId);
        
        if (sessions.length === 0) {
            const newSessionId = 'session_' + Date.now();
            sessions.push({
                id: newSessionId,
                title: 'New Chat',
                messages: [],
                timestamp: Date.now()
            });
            currentSessionId = newSessionId;
        } else if (currentSessionId === sessionId) {
            currentSessionId = sessions[0].id;
        }

        saveSessions();
        renderSessionsList();
        renderActiveSessionMessages();
        showToast('Conversation deleted.', 'info', 2000);
    }

    function saveToHistory(message) {
        const activeSession = sessions.find(s => s.id === currentSessionId);
        if (activeSession) {
            activeSession.messages.push(message);
            
            // Name the chat after the first user message
            if (activeSession.title === 'New Chat' && message.role === 'user') {
                let text = message.content.trim();
                if (text.length > 24) {
                    text = text.substring(0, 22) + '...';
                }
                activeSession.title = text;
            }
            
            saveSessions();
            renderSessionsList();
        }
    }

    function clearHistory() {
        const activeSession = sessions.find(s => s.id === currentSessionId);
        if (activeSession) {
            activeSession.messages = [];
            activeSession.title = 'New Chat';
            saveSessions();
            renderSessionsList();
            renderActiveSessionMessages();
            appendSystemMessage('Chat history cleared.');
        }
    }

    // UI-only appenders to prevent double-saving to history array during reload
    function appendMsgUI(role, content, sources = []) {
        const messageRow = document.createElement('div');
        messageRow.className = `message-row message-row-${role}`;

        const messageBubble = document.createElement('div');
        messageBubble.className = `message-bubble message-bubble-${role}`;
        messageBubble.innerHTML = formatMessageContent(content);

        if (role === 'ai' && sources && sources.length > 0) {
            const metaDiv = document.createElement('div');
            metaDiv.className = 'message-meta';
            
            const sourcesContainer = document.createElement('div');
            sourcesContainer.className = 'sources-container';
            
            const label = document.createElement('span');
            label.className = 'sources-label';
            label.textContent = 'Sources: ';
            sourcesContainer.appendChild(label);
            
            sources.forEach(src => {
                const badge = document.createElement('span');
                badge.className = 'source-badge';
                badge.textContent = src;
                sourcesContainer.appendChild(badge);
            });

            metaDiv.appendChild(sourcesContainer);
            messageBubble.appendChild(metaDiv);
        }

        messageRow.appendChild(messageBubble);
        chatFeed.appendChild(messageRow);
    }

    function appendSystemMsgUI(content) {
        const messageRow = document.createElement('div');
        messageRow.className = 'message-row message-row-system';

        const messageBubble = document.createElement('div');
        messageBubble.className = 'message-bubble-system';
        messageBubble.textContent = content;

        messageRow.appendChild(messageBubble);
        chatFeed.appendChild(messageRow);
    }

    // --- Toast System ---
    function showToast(message, type = 'info', duration = 4000) {
        const container = document.getElementById('toast-container');
        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        
        // Get visual icon for toast type
        let iconHtml = '';
        if (type === 'success') {
            iconHtml = `<svg viewBox="0 0 24 24" fill="none" stroke="var(--color-success)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="width:18px;height:18px;flex-shrink:0;"><polyline points="20 6 9 17 4 12"></polyline></svg>`;
        } else if (type === 'error') {
            iconHtml = `<svg viewBox="0 0 24 24" fill="none" stroke="var(--color-danger)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="width:18px;height:18px;flex-shrink:0;"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>`;
        } else {
            iconHtml = `<svg viewBox="0 0 24 24" fill="none" stroke="var(--color-primary)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="width:18px;height:18px;flex-shrink:0;"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>`;
        }

        toast.innerHTML = `
            ${iconHtml}
            <div>${message}</div>
        `;
        
        container.appendChild(toast);
        
        // Animate out and remove
        setTimeout(() => {
            toast.style.animation = 'slideInRight 0.3s reverse forwards';
            toast.addEventListener('animationend', () => {
                toast.remove();
            });
        }, duration);
    }

    // --- Search History Management ---

    function loadSearchHistory() {
        const stored = localStorage.getItem(SEARCH_HISTORY_KEY);
        if (stored) {
            try {
                searchHistory = JSON.parse(stored);
            } catch (e) {
                console.error('Failed to parse search history:', e);
                localStorage.removeItem(SEARCH_HISTORY_KEY);
                searchHistory = [];
            }
        }
        renderSearchHistory();
    }

    function renderSearchHistory() {
        searchHistoryList.innerHTML = '';
        if (searchHistory.length === 0) {
            searchHistoryList.innerHTML = '<div class="search-history-empty">No recent searches</div>';
            return;
        }

        searchHistory.forEach(query => {
            const chip = document.createElement('div');
            chip.className = 'history-item-chip';
            chip.setAttribute('data-query', query);

            // Escape HTML for safe rendering
            const escapedQuery = query
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#039;");

            chip.innerHTML = `
                <span class="history-item-text" title="${escapedQuery}">${escapedQuery}</span>
                <button class="history-item-delete" title="Remove from history">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <line x1="18" y1="6" x2="6" y2="18"></line>
                        <line x1="6" y1="6" x2="18" y2="18"></line>
                    </svg>
                </button>
            `;
            searchHistoryList.appendChild(chip);
        });
    }

    function addToSearchHistory(query) {
        if (!query || !query.trim()) return;
        const trimmed = query.trim();

        // Limit the search history items to not repeat duplicate inputs (case insensitive checks)
        searchHistory = searchHistory.filter(q => q.toLowerCase() !== trimmed.toLowerCase());

        // Push to front
        searchHistory.unshift(trimmed);

        // Keep maximum of 5 items
        if (searchHistory.length > 5) {
            searchHistory.pop();
        }

        localStorage.setItem(SEARCH_HISTORY_KEY, JSON.stringify(searchHistory));
        renderSearchHistory();
    }

    function deleteSearchHistoryItem(query) {
        searchHistory = searchHistory.filter(q => q !== query);
        localStorage.setItem(SEARCH_HISTORY_KEY, JSON.stringify(searchHistory));
        renderSearchHistory();
        showToast('Removed search query from history.', 'info', 2000);
    }

    function clearSearchHistory() {
        if (searchHistory.length === 0) return;
        if (confirm('Are you sure you want to clear your recent searches?')) {
            searchHistory = [];
            localStorage.removeItem(SEARCH_HISTORY_KEY);
            renderSearchHistory();
            showToast('Search history cleared.', 'success', 2000);
        }
    }
});
