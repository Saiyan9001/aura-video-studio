import { describe, it, expect, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { ActivityProvider, useActivity } from '../activityContext';

describe('ActivityContext', () => {
  it('should add a new activity', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    act(() => {
      result.current.addActivity({
        type: 'video-generation',
        title: 'Test Activity',
        message: 'Testing',
      });
    });

    expect(result.current.activities).toHaveLength(1);
    expect(result.current.activities[0].title).toBe('Test Activity');
    expect(result.current.activities[0].status).toBe('pending');
    expect(result.current.activities[0].progress).toBe(0);
  });

  it('should update an activity', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    let activityId: string;

    act(() => {
      activityId = result.current.addActivity({
        type: 'video-generation',
        title: 'Test Activity',
        message: 'Testing',
      });
    });

    act(() => {
      result.current.updateActivity(activityId, {
        status: 'running',
        progress: 50,
        message: 'Half done',
      });
    });

    expect(result.current.activities[0].status).toBe('running');
    expect(result.current.activities[0].progress).toBe(50);
    expect(result.current.activities[0].message).toBe('Half done');
  });

  it('should track active activities', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    let activityId1: string;
    let activityId2: string;

    act(() => {
      activityId1 = result.current.addActivity({
        type: 'video-generation',
        title: 'Activity 1',
        message: 'Testing',
      });
      activityId2 = result.current.addActivity({
        type: 'api-call',
        title: 'Activity 2',
        message: 'Testing',
      });
    });

    act(() => {
      result.current.updateActivity(activityId1, { status: 'running' });
      result.current.updateActivity(activityId2, { status: 'completed' });
    });

    expect(result.current.activeActivities).toHaveLength(1);
    expect(result.current.completedActivities).toHaveLength(1);
  });

  it('should remove an activity', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    let activityId: string;

    act(() => {
      activityId = result.current.addActivity({
        type: 'video-generation',
        title: 'Test Activity',
        message: 'Testing',
      });
    });

    expect(result.current.activities).toHaveLength(1);

    act(() => {
      result.current.removeActivity(activityId);
    });

    expect(result.current.activities).toHaveLength(0);
  });

  it('should clear completed activities', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    let activityId1: string;
    let activityId2: string;

    act(() => {
      activityId1 = result.current.addActivity({
        type: 'video-generation',
        title: 'Activity 1',
        message: 'Testing',
      });
      activityId2 = result.current.addActivity({
        type: 'api-call',
        title: 'Activity 2',
        message: 'Testing',
      });
    });

    act(() => {
      result.current.updateActivity(activityId1, { status: 'completed' });
      result.current.updateActivity(activityId2, { status: 'running' });
    });

    expect(result.current.activities).toHaveLength(2);

    act(() => {
      result.current.clearCompleted();
    });

    expect(result.current.activities).toHaveLength(1);
    expect(result.current.activities[0].status).toBe('running');
  });

  it('should set endTime when activity completes', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    let activityId: string;

    act(() => {
      activityId = result.current.addActivity({
        type: 'video-generation',
        title: 'Test Activity',
        message: 'Testing',
      });
    });

    expect(result.current.activities[0].endTime).toBeUndefined();

    act(() => {
      result.current.updateActivity(activityId, { status: 'completed' });
    });

    expect(result.current.activities[0].endTime).toBeDefined();
  });

  it('should pause and resume activities', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    let activityId: string;

    act(() => {
      activityId = result.current.addActivity({
        type: 'video-generation',
        title: 'Test Activity',
        message: 'Testing',
        canPause: true,
      });
    });

    act(() => {
      result.current.updateActivity(activityId, { status: 'running' });
    });

    expect(result.current.activities[0].status).toBe('running');

    act(() => {
      result.current.pauseActivity(activityId);
    });

    expect(result.current.activities[0].status).toBe('paused');

    act(() => {
      result.current.resumeActivity(activityId);
    });

    expect(result.current.activities[0].status).toBe('running');
  });

  it('should set and update activity priority', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    let activityId: string;

    act(() => {
      activityId = result.current.addActivity({
        type: 'video-generation',
        title: 'Test Activity',
        message: 'Testing',
        priority: 5,
      });
    });

    expect(result.current.activities[0].priority).toBe(5);

    act(() => {
      result.current.setPriority(activityId, 8);
    });

    expect(result.current.activities[0].priority).toBe(8);
  });

  it('should track queued activities sorted by priority', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    act(() => {
      result.current.addActivity({
        type: 'video-generation',
        title: 'Low Priority',
        message: 'Testing',
        priority: 3,
      });
      result.current.addActivity({
        type: 'api-call',
        title: 'High Priority',
        message: 'Testing',
        priority: 9,
      });
      result.current.addActivity({
        type: 'analysis',
        title: 'Medium Priority',
        message: 'Testing',
        priority: 6,
      });
    });

    expect(result.current.queuedActivities).toHaveLength(3);
    expect(result.current.queuedActivities[0].title).toBe('High Priority');
    expect(result.current.queuedActivities[1].title).toBe('Medium Priority');
    expect(result.current.queuedActivities[2].title).toBe('Low Priority');
  });

  it('should add to operation history when activity completes', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    let activityId: string;

    act(() => {
      activityId = result.current.addActivity({
        type: 'video-generation',
        title: 'Test Activity',
        message: 'Testing',
        category: 'export',
      });
    });

    expect(result.current.operationHistory).toHaveLength(0);

    act(() => {
      result.current.updateActivity(activityId, { status: 'completed' });
    });

    expect(result.current.operationHistory).toHaveLength(1);
    expect(result.current.operationHistory[0].title).toBe('Test Activity');
    expect(result.current.operationHistory[0].status).toBe('completed');
  });

  it('should clear operation history', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    act(() => {
      const id = result.current.addActivity({
        type: 'video-generation',
        title: 'Test Activity',
        message: 'Testing',
      });
      result.current.updateActivity(id, { status: 'completed' });
    });

    expect(result.current.operationHistory).toHaveLength(1);

    act(() => {
      result.current.clearOperationHistory();
    });

    expect(result.current.operationHistory).toHaveLength(0);
  });

  it('should provide resource usage data', () => {
    const { result } = renderHook(() => useActivity(), {
      wrapper: ActivityProvider,
    });

    // Wait for initial resource update
    act(() => {
      vi.useFakeTimers();
      vi.advanceTimersByTime(100);
    });

    expect(result.current.resourceUsage).toBeDefined();
    if (result.current.resourceUsage) {
      expect(result.current.resourceUsage.cpu).toBeGreaterThanOrEqual(0);
      expect(result.current.resourceUsage.cpu).toBeLessThanOrEqual(100);
      expect(result.current.resourceUsage.memory).toBeGreaterThanOrEqual(0);
      expect(result.current.resourceUsage.memory).toBeLessThanOrEqual(100);
    }

    vi.useRealTimers();
  });
});

