class ChatBox {
    constructor() {
        this.sessionId = this.generateSessionId();
        this.isOpen = false;
        this.isTyping = false;
        this.hasShownQuickReplies = false; // Thêm biến để theo dõi đã hiển thị quick replies chưa
        this.init();
    }

    init() {
        this.bindEvents();
        this.loadChatHistory();
    }

    generateSessionId() {
        return localStorage.getItem('chatSessionId') || this.createNewSession();
    }

    createNewSession() {
        const sessionId = 'chat_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
        localStorage.setItem('chatSessionId', sessionId);
        return sessionId;
    }

    bindEvents() {
        const chatBoxToggle = document.getElementById('chatBoxToggle');
        const chatToggle = document.getElementById('chatToggle');
        const chatInput = document.getElementById('chatInput');
        const sendButton = document.getElementById('sendButton');

        if (chatBoxToggle) {
            chatBoxToggle.addEventListener('click', () => this.toggleChatBox());
        }

        if (chatToggle) {
            chatToggle.addEventListener('click', () => this.minimizeChatBox());
        }

        if (chatInput) {
            chatInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    this.sendMessage();
                }
            });

            chatInput.addEventListener('input', () => {
                this.toggleSendButton();
            });
        }

        if (sendButton) {
            sendButton.addEventListener('click', () => this.sendMessage());
        }
    }

    toggleChatBox() {
        const chatBox = document.getElementById('chatBox');
        const chatBoxToggle = document.getElementById('chatBoxToggle');
        const notification = document.getElementById('chatNotification');

        if (chatBox && chatBoxToggle) {
            this.isOpen = !this.isOpen;

            if (this.isOpen) {
                chatBox.classList.add('active');
                chatBoxToggle.classList.add('active');
                chatBoxToggle.innerHTML = '<i class="fas fa-times"></i>';

                // Hide notification
                if (notification) {
                    notification.style.display = 'none';
                }

                // Focus input
                setTimeout(() => {
                    const chatInput = document.getElementById('chatInput');
                    if (chatInput) chatInput.focus();
                }, 300);

            } else {
                chatBox.classList.remove('active');
                chatBoxToggle.classList.remove('active');
                chatBoxToggle.innerHTML = '<i class="fas fa-comments"></i>';
            }
        }
    }

    minimizeChatBox() {
        const chatBox = document.getElementById('chatBox');
        const chatToggle = document.getElementById('chatToggle');

        if (chatBox && chatToggle) {
            const isMinimized = chatBox.classList.contains('minimized');

            if (isMinimized) {
                chatBox.classList.remove('minimized');
                chatToggle.innerHTML = '<i class="fas fa-minus"></i>';
            } else {
                chatBox.classList.add('minimized');
                chatToggle.innerHTML = '<i class="fas fa-plus"></i>';
            }
        }
    }

    toggleSendButton() {
        const chatInput = document.getElementById('chatInput');
        const sendButton = document.getElementById('sendButton');

        if (chatInput && sendButton) {
            const hasText = chatInput.value.trim().length > 0;
            sendButton.disabled = !hasText || this.isTyping;
        }
    }

    async sendMessage() {
        const chatInput = document.getElementById('chatInput');
        const message = chatInput?.value.trim();

        if (!message || this.isTyping) return;

        // Clear input
        chatInput.value = '';
        this.toggleSendButton();

        // Add user message to chat
        this.addMessage(message, true);

        // Show typing indicator
        this.showTyping();

        try {
            const response = await fetch('/api/chat/send', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                },
                body: JSON.stringify({
                    message: message,
                    sessionId: this.sessionId
                })
            });

            const data = await response.json();

            if (data.success) {
                // Update session ID if new
                if (data.sessionId && data.sessionId !== this.sessionId) {
                    this.sessionId = data.sessionId;
                    localStorage.setItem('chatSessionId', this.sessionId);
                }

                // Add AI response
                setTimeout(() => {
                    this.hideTyping();
                    this.addMessage(data.response, false);
                }, 1000); // Simulate typing delay
            } else {
                this.hideTyping();
                this.showError(data.message || 'Có lỗi xảy ra khi gửi tin nhắn');
            }
        } catch (error) {
            console.error('Chat error:', error);
            this.hideTyping();
            this.showError('Không thể kết nối đến server. Vui lòng thử lại.');
        }
    }

    addMessage(message, isUser = false) {
        const chatMessages = document.getElementById('chatMessages');
        if (!chatMessages) return;

        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${isUser ? 'user-message' : 'ai-message'}`;

        const currentTime = new Date().toLocaleTimeString('vi-VN', {
            hour: '2-digit',
            minute: '2-digit'
        });

        messageDiv.innerHTML = `
            <div class="message-avatar">
                <i class="fas ${isUser ? 'fa-user' : 'fa-robot'}"></i>
            </div>
            <div class="message-content">
                <p>${this.formatMessage(message)}</p>
                <span class="message-time">${currentTime}</span>
            </div>
        `;

        chatMessages.appendChild(messageDiv);
        this.scrollToBottom();

        // Chỉ thêm quick replies nếu:
        // 1. Đây là tin nhắn AI (không phải tin nhắn người dùng)
        // 2. Chưa hiển thị quick replies trước đó
        // 3. Không có tin nhắn từ người dùng nào trong lịch sử chat
        if (!isUser && !this.hasShownQuickReplies) {
            // Xóa quick replies hiện có (nếu có)
            const existingReplies = chatMessages.querySelector('.quick-replies');
            if (existingReplies) {
                existingReplies.remove();
            }

            // Thêm quick replies mới
            this.addQuickReplies();

            // Đánh dấu đã hiển thị quick replies
            this.hasShownQuickReplies = true;
        }
    }

    formatMessage(message) {
        // Basic formatting for links and line breaks
        return message
            .replace(/\n/g, '<br>')
            .replace(/(https?:\/\/[^\s]+)/g, '<a href="$1" target="_blank" rel="noopener">$1</a>');
    }

    addQuickReplies() {
        const chatMessages = document.getElementById('chatMessages');
        if (!chatMessages) return;

        const quickReplies = [
            'Xem sản phẩm mới',
            'Thông tin giao hàng',
            'Chính sách đổi trả',
            'Liên hệ hỗ trợ'
        ];

        const repliesDiv = document.createElement('div');
        repliesDiv.className = 'quick-replies';

        quickReplies.forEach(reply => {
            const btn = document.createElement('button');
            btn.className = 'quick-reply-btn';
            btn.textContent = reply;
            btn.addEventListener('click', () => {
                document.getElementById('chatInput').value = reply;
                this.sendMessage();
            });
            repliesDiv.appendChild(btn);
        });

        chatMessages.appendChild(repliesDiv);
        this.scrollToBottom();
    }

    showTyping() {
        this.isTyping = true;
        const chatTyping = document.getElementById('chatTyping');
        if (chatTyping) {
            chatTyping.style.display = 'flex';
        }
        this.toggleSendButton();
    }

    hideTyping() {
        this.isTyping = false;
        const chatTyping = document.getElementById('chatTyping');
        if (chatTyping) {
            chatTyping.style.display = 'none';
        }
        this.toggleSendButton();
    }

    showError(message) {
        const chatMessages = document.getElementById('chatMessages');
        if (!chatMessages) return;

        const errorDiv = document.createElement('div');
        errorDiv.className = 'error-message';
        errorDiv.textContent = message;

        chatMessages.appendChild(errorDiv);
        this.scrollToBottom();

        // Auto remove error after 5 seconds
        setTimeout(() => {
            if (errorDiv.parentNode) {
                errorDiv.remove();
            }
        }, 5000);
    }

    scrollToBottom() {
        const chatMessages = document.getElementById('chatMessages');
        if (chatMessages) {
            setTimeout(() => {
                chatMessages.scrollTop = chatMessages.scrollHeight;
            }, 100);
        }
    }

    async loadChatHistory() {
        try {
            const response = await fetch(`/api/chat/history/${this.sessionId}`);
            if (response.ok) {
                const history = await response.json();

                // Clear existing messages except welcome message
                const chatMessages = document.getElementById('chatMessages');
                if (chatMessages && history.length > 0) {
                    // Keep only the welcome message
                    const welcomeMessage = chatMessages.querySelector('.ai-message');
                    chatMessages.innerHTML = '';
                    if (welcomeMessage) {
                        chatMessages.appendChild(welcomeMessage);
                    }

                    // Add history messages
                    history.forEach(msg => {
                        this.addMessage(msg.message, msg.isFromUser);
                    });

                    // Đánh dấu là đã hiển thị quick replies nếu trong lịch sử đã có tin nhắn từ người dùng
                    if (history.some(msg => msg.isFromUser)) {
                        this.hasShownQuickReplies = true;
                    } else {
                        // Nếu không có tin nhắn từ người dùng, hiển thị quick replies mặc định
                        this.hasShownQuickReplies = false;
                        this.addQuickReplies();
                        this.hasShownQuickReplies = true;
                    }
                } else {
                    // Không có lịch sử chat, hiển thị quick replies mặc định
                    this.addQuickReplies();
                    this.hasShownQuickReplies = true;
                }
            }
        } catch (error) {
            console.error('Error loading chat history:', error);
        }
    }

    // Show notification when chat is closed
    showNotification() {
        if (!this.isOpen) {
            const notification = document.getElementById('chatNotification');
            if (notification) {
                notification.style.display = 'block';
                notification.textContent = '1';
            }
        }
    }
}

// Initialize chat box when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    window.chatBox = new ChatBox();

    // Show notification after 10 seconds if chat hasn't been opened
    setTimeout(() => {
        if (!window.chatBox.isOpen) {
            window.chatBox.showNotification();
        }
    }, 10000);
});