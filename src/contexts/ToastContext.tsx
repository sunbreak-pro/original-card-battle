/**
 * ToastContext
 *
 * Manages toast notifications for achievements, alerts, and feedback.
 * Auto-dismisses toasts after their duration expires.
 */

import React, {
  createContext,
  useContext,
  useState,
  useCallback,
  useRef,
  type ReactNode,
} from 'react';

// ============================================================================
// Types
// ============================================================================

export type ToastType = 'achievement' | 'success' | 'alert' | 'error';

export interface Toast {
  id: string;
  message: string;
  type: ToastType;
  duration: number;       // milliseconds
  icon?: string;          // Emoji or icon
  title?: string;         // Optional title (for achievements)
}

export type ToastInput = Omit<Toast, 'id'>;

interface ToastContextValue {
  toasts: Toast[];
  addToast: (toast: ToastInput) => string;
  removeToast: (id: string) => void;
  clearAllToasts: () => void;
}

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_DURATIONS: Record<ToastType, number> = {
  achievement: 5000,
  success: 3000,
  alert: 4000,
  error: 5000,
};

const MAX_TOASTS = 3;

// ============================================================================
// Context
// ============================================================================

const ToastContext = createContext<ToastContextValue | undefined>(undefined);

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Generate unique toast ID
 */
function generateToastId(): string {
  return `toast_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`;
}

// ============================================================================
// Provider Component
// ============================================================================

export const ToastProvider: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const timersRef = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map());

  /**
   * Remove a toast by ID
   */
  const removeToast = useCallback((id: string) => {
    // Clear the timer if it exists
    const timer = timersRef.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timersRef.current.delete(id);
    }

    setToasts((prev) => prev.filter((toast) => toast.id !== id));
  }, []);

  /**
   * Add a new toast
   * @returns The toast ID
   */
  const addToast = useCallback(
    (input: ToastInput): string => {
      const id = generateToastId();
      const duration = input.duration || DEFAULT_DURATIONS[input.type];

      const newToast: Toast = {
        ...input,
        id,
        duration,
      };

      setToasts((prev) => {
        // Limit to MAX_TOASTS, remove oldest if exceeded
        const updated = [...prev, newToast];
        if (updated.length > MAX_TOASTS) {
          const removed = updated.shift();
          if (removed) {
            const timer = timersRef.current.get(removed.id);
            if (timer) {
              clearTimeout(timer);
              timersRef.current.delete(removed.id);
            }
          }
        }
        return updated;
      });

      // Set up auto-dismiss timer
      const timer = setTimeout(() => {
        removeToast(id);
      }, duration);

      timersRef.current.set(id, timer);

      return id;
    },
    [removeToast],
  );

  /**
   * Clear all toasts
   */
  const clearAllToasts = useCallback(() => {
    // Clear all timers
    timersRef.current.forEach((timer) => clearTimeout(timer));
    timersRef.current.clear();

    setToasts([]);
  }, []);

  return (
    <ToastContext.Provider
      value={{
        toasts,
        addToast,
        removeToast,
        clearAllToasts,
      }}
    >
      {children}
    </ToastContext.Provider>
  );
};

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook to use Toast context
 */
// eslint-disable-next-line react-refresh/only-export-components
export const useToast = () => {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within ToastProvider');
  }
  return context;
};

// ============================================================================
// Helper Hook for Achievement Toasts
// ============================================================================

/**
 * Hook to easily show achievement toasts
 */
// eslint-disable-next-line react-refresh/only-export-components
export const useAchievementToast = () => {
  const { addToast } = useToast();

  const showAchievement = useCallback(
    (name: string, description: string, icon?: string) => {
      return addToast({
        type: 'achievement',
        title: name,
        message: description,
        icon: icon || '🏆',
        duration: 5000,
      });
    },
    [addToast],
  );

  return { showAchievement };
};
