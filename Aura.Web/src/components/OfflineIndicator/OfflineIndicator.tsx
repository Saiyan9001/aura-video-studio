/**
 * Offline Indicator Component
 * Displays a warning when the application goes offline
 */

import { MessageBar, MessageBarBody, MessageBarTitle } from '@fluentui/react-components';
import { WifiWarning24Regular } from '@fluentui/react-icons';
import { useOnlineStatus } from '../../hooks/useOnlineStatus';

/**
 * Offline indicator component
 */
export function OfflineIndicator(): JSX.Element | null {
  const isOnline = useOnlineStatus();

  if (isOnline) {
    return null;
  }

  return (
    <MessageBar
      intent="warning"
      style={{ position: 'fixed', top: 0, left: 0, right: 0, zIndex: 9999 }}
    >
      <MessageBarBody>
        <MessageBarTitle>
          <WifiWarning24Regular style={{ marginRight: '0.5rem' }} />
          No Internet Connection
        </MessageBarTitle>
        You are currently offline. Some features may not be available.
      </MessageBarBody>
    </MessageBar>
  );
}
