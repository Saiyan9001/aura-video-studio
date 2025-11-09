import { describe, it, expect, beforeEach } from 'vitest';
import { useNotificationStore } from '../../../state/notifications';

describe('NotificationStore', () => {
  beforeEach(() => {
    // Reset store before each test
    useNotificationStore.setState({
      notifications: [],
      unreadCount: 0,
      showDropdown: false,
    });
  });

  it('adds notification', () => {
    const { addNotification, notifications } = useNotificationStore.getState();

    addNotification({
      type: 'success',
      title: 'Test',
      message: 'Test message',
    });

    const state = useNotificationStore.getState();
    expect(state.notifications).toHaveLength(1);
    expect(state.notifications[0].title).toBe('Test');
    expect(state.notifications[0].type).toBe('success');
    expect(state.unreadCount).toBe(1);
  });

  it('marks notification as read', () => {
    const { addNotification, markAsRead } = useNotificationStore.getState();

    addNotification({
      type: 'info',
      title: 'Test',
      message: 'Test message',
    });

    const notification = useNotificationStore.getState().notifications[0];
    expect(notification.read).toBe(false);

    markAsRead(notification.id);

    const state = useNotificationStore.getState();
    expect(state.notifications[0].read).toBe(true);
    expect(state.unreadCount).toBe(0);
  });

  it('marks all as read', () => {
    const { addNotification, markAllAsRead } = useNotificationStore.getState();

    addNotification({
      type: 'success',
      title: 'Test 1',
      message: 'Message 1',
    });

    addNotification({
      type: 'warning',
      title: 'Test 2',
      message: 'Message 2',
    });

    expect(useNotificationStore.getState().unreadCount).toBe(2);

    markAllAsRead();

    const state = useNotificationStore.getState();
    expect(state.notifications.every((n) => n.read)).toBe(true);
    expect(state.unreadCount).toBe(0);
  });

  it('removes notification', () => {
    const { addNotification, removeNotification } = useNotificationStore.getState();

    addNotification({
      type: 'error',
      title: 'Test',
      message: 'Test message',
    });

    const notification = useNotificationStore.getState().notifications[0];
    expect(useNotificationStore.getState().notifications).toHaveLength(1);

    removeNotification(notification.id);

    expect(useNotificationStore.getState().notifications).toHaveLength(0);
  });

  it('clears all notifications', () => {
    const { addNotification, clearAll } = useNotificationStore.getState();

    addNotification({
      type: 'success',
      title: 'Test 1',
      message: 'Message 1',
    });

    addNotification({
      type: 'info',
      title: 'Test 2',
      message: 'Message 2',
    });

    expect(useNotificationStore.getState().notifications).toHaveLength(2);

    clearAll();

    const state = useNotificationStore.getState();
    expect(state.notifications).toHaveLength(0);
    expect(state.unreadCount).toBe(0);
  });

  it('toggles dropdown', () => {
    const { toggleDropdown } = useNotificationStore.getState();

    expect(useNotificationStore.getState().showDropdown).toBe(false);

    toggleDropdown();
    expect(useNotificationStore.getState().showDropdown).toBe(true);

    toggleDropdown();
    expect(useNotificationStore.getState().showDropdown).toBe(false);
  });

  it('updates unread count correctly', () => {
    const { addNotification, markAsRead } = useNotificationStore.getState();

    addNotification({
      type: 'success',
      title: 'Test 1',
      message: 'Message 1',
    });

    addNotification({
      type: 'success',
      title: 'Test 2',
      message: 'Message 2',
    });

    expect(useNotificationStore.getState().unreadCount).toBe(2);

    const firstNotification = useNotificationStore.getState().notifications[0];
    markAsRead(firstNotification.id);

    expect(useNotificationStore.getState().unreadCount).toBe(1);
  });
});
