import { MessageBar, MessageBarBody, Button, tokens } from '@fluentui/react-components';
import { Component, ReactNode } from 'react';
import { FFmpegCard } from './FFmpegCard';

interface Props {
  children?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class SafeFFmpegCard extends Component<Props, State> {
  public state: State = {
    hasError: false,
    error: null,
  };

  public static getDerivedStateFromError(error: Error): Partial<State> {
    return {
      hasError: true,
      error,
    };
  }

  public componentDidCatch(error: Error) {
    console.error('FFmpegCard error:', error);
  }

  private handleReset = () => {
    this.setState({
      hasError: false,
      error: null,
    });
  };

  public render() {
    if (this.state.hasError) {
      return (
        <MessageBar intent="warning">
          <MessageBarBody>
            FFmpeg configuration unavailable. The backend may not be running.
            <Button
              appearance="transparent"
              onClick={this.handleReset}
              style={{ marginLeft: tokens.spacingHorizontalM }}
            >
              Retry
            </Button>
          </MessageBarBody>
        </MessageBar>
      );
    }

    return <FFmpegCard />;
  }
}
