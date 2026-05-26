/**
 * ToastContainer
 *
 * Renders all active toast notifications.
 * Positioned at the top-center of the screen.
 */

import React from 'react';
import { useToast, type Toast } from '@/contexts/ToastContext';
import '@/ui/css/components/Toast.css';

// ============================================================================
// Toast Item Component
// ============================================================================

interface ToastItemProps {
  toast: Toast;
  onClose: () => void;
}

const ToastItem: React.FC<ToastItemProps> = ({ toast, onClose }) => {
  const isAchievement = toast.type === 'achievement';

  return (
    <div
      className={`toast toast-${toast.type}`}
      role="alert"
      aria-live="polite"
    >
      {toast.icon && (
        <span className="toast-icon">{toast.icon}</span>
      )}
      <div className="toast-content">
        {isAchievement && toast.title && (
          <div className="toast-title">{toast.title}</div>
        )}
        <div className="toast-message">{toast.message}</div>
      </div>
      <button
        className="toast-close"
        onClick={onClose}
        aria-label="閉じる"
      >
        ✕
      </button>
    </div>
  );
};

// ============================================================================
// Toast Container Component
// ============================================================================

export const ToastContainer: React.FC = () => {
  const { toasts, removeToast } = useToast();

  if (toasts.length === 0) {
    return null;
  }

  return (
    <div className="toast-container" aria-label="通知">
      {toasts.map((toast) => (
        <ToastItem
          key={toast.id}
          toast={toast}
          onClose={() => removeToast(toast.id)}
        />
      ))}
    </div>
  );
};

export default ToastContainer;
