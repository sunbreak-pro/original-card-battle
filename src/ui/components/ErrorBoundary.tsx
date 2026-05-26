import { Component } from 'react';
import type { ReactNode, ErrorInfo } from 'react';
import { logger } from '@/utils/logger';

interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

/**
 * Error Boundary component to catch JavaScript errors anywhere in the child component tree.
 * Logs errors and displays a fallback UI instead of crashing the whole app.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    logger.error('ErrorBoundary caught an error:', error, errorInfo.componentStack);
  }

  handleReset = (): void => {
    this.setState({ hasError: false, error: null });
  };

  render(): ReactNode {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }
      return (
        <DefaultErrorFallback
          error={this.state.error}
          onReset={this.handleReset}
        />
      );
    }

    return this.props.children;
  }
}

interface DefaultErrorFallbackProps {
  error: Error | null;
  onReset: () => void;
}

/**
 * Default fallback UI when an error occurs
 */
function DefaultErrorFallback({ error, onReset }: DefaultErrorFallbackProps): ReactNode {
  const isDev = import.meta.env.DEV;

  return (
    <div style={styles.container}>
      <div style={styles.content}>
        <h1 style={styles.title}>エラーが発生しました</h1>
        <p style={styles.message}>
          予期せぬエラーが発生しました。ページを再読み込みしてください。
        </p>
        {isDev && error && (
          <div style={styles.errorDetails}>
            <p style={styles.errorName}>{error.name}: {error.message}</p>
            <pre style={styles.errorStack}>{error.stack}</pre>
          </div>
        )}
        <div style={styles.buttonContainer}>
          <button style={styles.button} onClick={onReset}>
            再試行
          </button>
          <button style={styles.button} onClick={() => window.location.reload()}>
            ページを再読み込み
          </button>
        </div>
      </div>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: '100vh',
    backgroundColor: '#1a1a2e',
    color: '#e0e0e0',
    fontFamily: 'system-ui, sans-serif',
    padding: '2rem',
  },
  content: {
    maxWidth: '600px',
    textAlign: 'center',
  },
  title: {
    fontSize: '2rem',
    marginBottom: '1rem',
    color: '#ff6b6b',
  },
  message: {
    fontSize: '1.1rem',
    marginBottom: '2rem',
    lineHeight: '1.6',
  },
  errorDetails: {
    textAlign: 'left',
    backgroundColor: '#16213e',
    padding: '1rem',
    borderRadius: '8px',
    marginBottom: '2rem',
    border: '1px solid #ff6b6b',
  },
  errorName: {
    color: '#ff6b6b',
    fontWeight: 'bold',
    marginBottom: '0.5rem',
  },
  errorStack: {
    fontSize: '0.8rem',
    overflow: 'auto',
    maxHeight: '200px',
    color: '#a0a0a0',
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-word',
  },
  buttonContainer: {
    display: 'flex',
    gap: '1rem',
    justifyContent: 'center',
  },
  button: {
    padding: '0.75rem 1.5rem',
    fontSize: '1rem',
    backgroundColor: '#4a4e69',
    color: '#e0e0e0',
    border: 'none',
    borderRadius: '6px',
    cursor: 'pointer',
    transition: 'background-color 0.2s',
  },
};
