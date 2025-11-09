import {
  Toast,
  ToastTitle,
  ToastBody,
  Toaster,
  useToastController,
  useId,
  ToastIntent,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  Warning24Filled,
  ErrorCircle24Filled,
  Info24Filled,
} from '@fluentui/react-icons';
import { useEffect } from 'react';
import { useNotificationStore, type NotificationType } from '../../state/notifications';

export function NotificationToaster() {
  const toasterId = useId('toaster');
  const { dispatchToast } = useToastController(toasterId);
  const { notifications } = useNotificationStore();

  useEffect(() => {
    const latestNotification = notifications[0];
    if (latestNotification && !latestNotification.read) {
      const intent = getToastIntent(latestNotification.type);
      const icon = getIcon(latestNotification.type);

      dispatchToast(
        <Toast>
          <ToastTitle media={icon}>{latestNotification.title}</ToastTitle>
          <ToastBody>{latestNotification.message}</ToastBody>
        </Toast>,
        { intent, timeout: 5000 }
      );
    }
  }, [notifications, dispatchToast]);

  const getToastIntent = (type: NotificationType): ToastIntent => {
    switch (type) {
      case 'success':
        return 'success';
      case 'warning':
        return 'warning';
      case 'error':
        return 'error';
      case 'info':
        return 'info';
    }
  };

  const getIcon = (type: NotificationType) => {
    switch (type) {
      case 'success':
        return <CheckmarkCircle24Filled />;
      case 'warning':
        return <Warning24Filled />;
      case 'error':
        return <ErrorCircle24Filled />;
      case 'info':
        return <Info24Filled />;
    }
  };

  return <Toaster toasterId={toasterId} position="top-end" />;
}
