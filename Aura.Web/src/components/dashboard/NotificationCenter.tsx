import {
  makeStyles,
  tokens,
  Button,
  Popover,
  PopoverTrigger,
  PopoverSurface,
  Text,
  Badge,
  mergeClasses,
} from '@fluentui/react-components';
import {
  Alert24Regular,
  Dismiss24Regular,
  CheckmarkCircle24Filled,
  Warning24Filled,
  ErrorCircle24Filled,
  Info24Filled,
} from '@fluentui/react-icons';
import { useNotificationStore, type Notification } from '../../state/notifications';

const useStyles = makeStyles({
  trigger: {
    position: 'relative',
  },
  badge: {
    position: 'absolute',
    top: '-4px',
    right: '-4px',
    minWidth: '20px',
    height: '20px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: '10px',
    fontSize: '11px',
    fontWeight: tokens.fontWeightSemibold,
  },
  surface: {
    width: '400px',
    maxHeight: '500px',
    display: 'flex',
    flexDirection: 'column',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalM,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase300,
  },
  headerActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
  },
  notificationList: {
    overflowY: 'auto',
    flex: 1,
    padding: tokens.spacingVerticalS,
  },
  notificationItem: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingVerticalM,
    borderRadius: tokens.borderRadiusMedium,
    marginBottom: tokens.spacingVerticalS,
    cursor: 'pointer',
    transition: 'background-color 0.2s ease',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  notificationUnread: {
    backgroundColor: tokens.colorNeutralBackground2,
  },
  notificationIcon: {
    fontSize: '24px',
    flexShrink: 0,
  },
  notificationContent: {
    flex: 1,
    minWidth: 0,
  },
  notificationTitle: {
    fontWeight: tokens.fontWeightSemibold,
    marginBottom: tokens.spacingVerticalXXS,
  },
  notificationMessage: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    marginBottom: tokens.spacingVerticalXS,
  },
  notificationTime: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground4,
  },
  dismissButton: {
    flexShrink: 0,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
});

function NotificationItem({
  notification,
  onDismiss,
}: {
  notification: Notification;
  onDismiss: () => void;
}) {
  const styles = useStyles();
  const markAsRead = useNotificationStore((state) => state.markAsRead);

  const getIcon = () => {
    switch (notification.type) {
      case 'success':
        return (
          <CheckmarkCircle24Filled
            className={styles.notificationIcon}
            style={{ color: tokens.colorPaletteGreenForeground1 }}
          />
        );
      case 'warning':
        return (
          <Warning24Filled
            className={styles.notificationIcon}
            style={{ color: tokens.colorPaletteYellowForeground1 }}
          />
        );
      case 'error':
        return (
          <ErrorCircle24Filled
            className={styles.notificationIcon}
            style={{ color: tokens.colorPaletteRedForeground1 }}
          />
        );
      case 'info':
        return (
          <Info24Filled
            className={styles.notificationIcon}
            style={{ color: tokens.colorBrandBackground }}
          />
        );
    }
  };

  const formatTime = (timestamp: string) => {
    const date = new Date(timestamp);
    const now = new Date();
    const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (diffInSeconds < 60) return 'Just now';
    if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`;
    if (diffInSeconds < 86400) return `${Math.floor(diffInSeconds / 3600)}h ago`;
    return date.toLocaleDateString();
  };

  const handleClick = () => {
    if (!notification.read) {
      markAsRead(notification.id);
    }
    if (notification.action) {
      notification.action.onClick();
    }
  };

  return (
    <div
      className={mergeClasses(
        styles.notificationItem,
        !notification.read && styles.notificationUnread
      )}
      onClick={handleClick}
    >
      {getIcon()}
      <div className={styles.notificationContent}>
        <div className={styles.notificationTitle}>{notification.title}</div>
        <div className={styles.notificationMessage}>{notification.message}</div>
        <div className={styles.notificationTime}>{formatTime(notification.timestamp)}</div>
        {notification.action && (
          <Button size="small" appearance="primary" style={{ marginTop: tokens.spacingVerticalXS }}>
            {notification.action.label}
          </Button>
        )}
      </div>
      <Button
        appearance="transparent"
        icon={<Dismiss24Regular />}
        size="small"
        className={styles.dismissButton}
        onClick={(e) => {
          e.stopPropagation();
          onDismiss();
        }}
        aria-label="Dismiss notification"
      />
    </div>
  );
}

export function NotificationCenter() {
  const styles = useStyles();
  const {
    notifications,
    unreadCount,
    showDropdown,
    markAllAsRead,
    clearAll,
    removeNotification,
    setShowDropdown,
  } = useNotificationStore();

  return (
    <Popover
      open={showDropdown}
      onOpenChange={(_e, data) => setShowDropdown(data.open)}
      positioning="below-end"
    >
      <PopoverTrigger disableButtonEnhancement>
        <div className={styles.trigger}>
          <Button appearance="transparent" icon={<Alert24Regular />} aria-label="Notifications" />
          {unreadCount > 0 && (
            <Badge className={styles.badge} appearance="filled" color="danger" size="small">
              {unreadCount > 99 ? '99+' : unreadCount}
            </Badge>
          )}
        </div>
      </PopoverTrigger>
      <PopoverSurface className={styles.surface}>
        <div className={styles.header}>
          <Text className={styles.title}>Notifications</Text>
          <div className={styles.headerActions}>
            {notifications.length > 0 && (
              <>
                <Button size="small" appearance="transparent" onClick={markAllAsRead}>
                  Mark all read
                </Button>
                <Button size="small" appearance="transparent" onClick={clearAll}>
                  Clear all
                </Button>
              </>
            )}
          </div>
        </div>
        <div className={styles.notificationList}>
          {notifications.length === 0 ? (
            <div className={styles.emptyState}>
              <Text>No notifications</Text>
            </div>
          ) : (
            notifications.map((notification) => (
              <NotificationItem
                key={notification.id}
                notification={notification}
                onDismiss={() => removeNotification(notification.id)}
              />
            ))
          )}
        </div>
      </PopoverSurface>
    </Popover>
  );
}
