import React, { useState, useEffect, useRef, useCallback } from 'react';
import { conversationService, Message } from '../../services/conversationService';

interface ConversationPanelProps {
  projectId: string;
  onClose?: () => void;
}

export const ConversationPanel: React.FC<ConversationPanelProps> = ({ projectId, onClose }) => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [inputMessage, setInputMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  const loadHistory = useCallback(async () => {
    try {
      const response = await conversationService.getHistory(projectId);
      setMessages(response.messages);
      setError(null);
    } catch (err) {
      setError('Failed to load conversation history');
      console.error('Error loading history:', err);
    }
  }, [projectId]);

  // Load conversation history on mount
  useEffect(() => {
    loadHistory();
  }, [loadHistory]);

  // Auto-scroll to bottom when new messages arrive
  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const handleSendMessage = async () => {
    if (!inputMessage.trim() || isLoading) return;

    const userMessage = inputMessage.trim();
    setInputMessage('');
    setIsLoading(true);
    setError(null);

    try {
      // Optimistically add user message
      const tempUserMessage: Message = {
        role: 'user',
        content: userMessage,
        timestamp: new Date().toISOString(),
      };
      setMessages((prev) => [...prev, tempUserMessage]);

      // Send message and get response
      const response = await conversationService.sendMessage(projectId, userMessage);

      // Add assistant response
      const assistantMessage: Message = {
        role: 'assistant',
        content: response.response,
        timestamp: response.timestamp,
      };
      setMessages((prev) => [...prev, assistantMessage]);
    } catch (err) {
      setError('Failed to send message');
      console.error('Error sending message:', err);
      // Remove optimistic user message on error
      setMessages((prev) => prev.slice(0, -1));
    } finally {
      setIsLoading(false);
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  const handleClearHistory = async () => {
    if (!confirm('Are you sure you want to clear the conversation history?')) {
      return;
    }

    try {
      await conversationService.clearConversation(projectId);
      setMessages([]);
      setError(null);
    } catch (err) {
      setError('Failed to clear conversation');
      console.error('Error clearing conversation:', err);
    }
  };

  return (
    <div className="conversation-panel">
      {/* Header */}
      <div className="conversation-header">
        <h2>AI Assistant</h2>
        <div className="conversation-actions">
          <button
            onClick={handleClearHistory}
            className="btn-clear"
            title="Clear conversation history"
            disabled={messages.length === 0}
          >
            Clear History
          </button>
          {onClose && (
            <button onClick={onClose} className="btn-close" title="Close">
              ×
            </button>
          )}
        </div>
      </div>

      {/* Error display */}
      {error && (
        <div className="conversation-error">
          <span className="error-icon">⚠️</span>
          {error}
        </div>
      )}

      {/* Messages display */}
      <div className="conversation-messages">
        {messages.length === 0 && !isLoading && (
          <div className="conversation-empty">
            <p>No messages yet. Start a conversation with the AI assistant!</p>
          </div>
        )}

        {messages.map((message, index) => (
          <div key={index} className={`message message-${message.role}`}>
            <div className="message-header">
              <span className="message-role">
                {message.role === 'user' ? 'You' : 'AI Assistant'}
              </span>
              <span className="message-timestamp">
                {conversationService.formatTimestamp(message.timestamp)}
              </span>
            </div>
            <div className="message-content">{message.content}</div>
          </div>
        ))}

        {isLoading && (
          <div className="message message-assistant">
            <div className="message-header">
              <span className="message-role">AI Assistant</span>
            </div>
            <div className="message-content message-loading">
              <span className="loading-dots">Thinking</span>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input area */}
      <div className="conversation-input">
        <textarea
          value={inputMessage}
          onChange={(e) => setInputMessage(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Type your message... (Shift+Enter for new line)"
          rows={3}
          disabled={isLoading}
          className="message-textarea"
        />
        <button
          onClick={handleSendMessage}
          disabled={!inputMessage.trim() || isLoading}
          className="btn-send"
        >
          {isLoading ? 'Sending...' : 'Send'}
        </button>
      </div>

      <style>{`
        .conversation-panel {
          display: flex;
          flex-direction: column;
          height: 100%;
          background: var(--bg-primary, #ffffff);
          border-radius: 8px;
          box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
        }

        .conversation-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 1rem;
          border-bottom: 1px solid var(--border-color, #e0e0e0);
        }

        .conversation-header h2 {
          margin: 0;
          font-size: 1.25rem;
          font-weight: 600;
        }

        .conversation-actions {
          display: flex;
          gap: 0.5rem;
        }

        .btn-clear,
        .btn-close {
          padding: 0.5rem 1rem;
          border: 1px solid var(--border-color, #e0e0e0);
          background: transparent;
          border-radius: 4px;
          cursor: pointer;
          font-size: 0.875rem;
          transition: all 0.2s;
        }

        .btn-clear:hover:not(:disabled),
        .btn-close:hover {
          background: var(--bg-hover, #f5f5f5);
        }

        .btn-clear:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }

        .btn-close {
          font-size: 1.5rem;
          padding: 0.25rem 0.75rem;
          line-height: 1;
        }

        .conversation-error {
          padding: 0.75rem 1rem;
          background: #fee;
          color: #c33;
          display: flex;
          align-items: center;
          gap: 0.5rem;
          border-bottom: 1px solid #fcc;
        }

        .conversation-messages {
          flex: 1;
          overflow-y: auto;
          padding: 1rem;
          display: flex;
          flex-direction: column;
          gap: 1rem;
        }

        .conversation-empty {
          flex: 1;
          display: flex;
          align-items: center;
          justify-content: center;
          color: var(--text-secondary, #666);
          text-align: center;
        }

        .message {
          padding: 0.75rem;
          border-radius: 8px;
          max-width: 80%;
        }

        .message-user {
          align-self: flex-end;
          background: var(--primary-color, #007bff);
          color: white;
        }

        .message-assistant {
          align-self: flex-start;
          background: var(--bg-secondary, #f5f5f5);
          color: var(--text-primary, #333);
        }

        .message-system {
          align-self: center;
          background: var(--bg-tertiary, #fff3cd);
          color: var(--text-primary, #333);
          max-width: 90%;
        }

        .message-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 0.5rem;
          font-size: 0.75rem;
          opacity: 0.8;
        }

        .message-role {
          font-weight: 600;
        }

        .message-timestamp {
          font-size: 0.7rem;
        }

        .message-content {
          white-space: pre-wrap;
          word-wrap: break-word;
          line-height: 1.5;
        }

        .message-loading {
          font-style: italic;
          opacity: 0.7;
        }

        .loading-dots::after {
          content: '';
          animation: dots 1.5s steps(4, end) infinite;
        }

        @keyframes dots {
          0%, 20% { content: ''; }
          40% { content: '.'; }
          60% { content: '..'; }
          80%, 100% { content: '...'; }
        }

        .conversation-input {
          padding: 1rem;
          border-top: 1px solid var(--border-color, #e0e0e0);
          display: flex;
          gap: 0.5rem;
          align-items: flex-end;
        }

        .message-textarea {
          flex: 1;
          padding: 0.75rem;
          border: 1px solid var(--border-color, #e0e0e0);
          border-radius: 4px;
          font-family: inherit;
          font-size: 0.875rem;
          resize: vertical;
          min-height: 60px;
        }

        .message-textarea:focus {
          outline: none;
          border-color: var(--primary-color, #007bff);
        }

        .btn-send {
          padding: 0.75rem 1.5rem;
          background: var(--primary-color, #007bff);
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: 600;
          transition: all 0.2s;
        }

        .btn-send:hover:not(:disabled) {
          background: var(--primary-hover, #0056b3);
        }

        .btn-send:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }
      `}</style>
    </div>
  );
};
