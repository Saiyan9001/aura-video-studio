/**
 * SystemStatusIndicator Component
 *
 * Displays system health status in the UI header
 * Shows degraded mode warnings and allows checking detailed status
 */

import {
  Badge,
  Button,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
  Tooltip,
} from '@fluentui/react-components';
import {
  CheckmarkCircle20Filled,
  Warning20Filled,
  DismissCircle20Filled,
  ChevronDown20Regular,
} from '@fluentui/react-icons';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getHealthSummary } from '../../services/api/healthApi';
import type { HealthSummaryResponse } from '../../types/api-v1';

const REFRESH_INTERVAL = 30000;

export function SystemStatusIndicator() {
  const navigate = useNavigate();
  const [health, setHealth] = useState<HealthSummaryResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchHealth = async () => {
    try {
      const summary = await getHealthSummary();
      setHealth(summary);
      setError(null);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch health status');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchHealth();
    const interval = setInterval(fetchHealth, REFRESH_INTERVAL);
    return () => clearInterval(interval);
  }, []);

  const getStatusIcon = () => {
    if (error) {
      return <DismissCircle20Filled style={{ color: '#D13438' }} />;
    }

    if (!health) {
      return <Warning20Filled style={{ color: '#797775' }} />;
    }

    if (health.overallStatus === 'Healthy') {
      return <CheckmarkCircle20Filled style={{ color: '#107C10' }} />;
    }

    if (health.overallStatus === 'Degraded') {
      return <Warning20Filled style={{ color: '#F7630C' }} />;
    }

    return <DismissCircle20Filled style={{ color: '#D13438' }} />;
  };

  const getStatusText = () => {
    if (error) {
      return 'Offline';
    }

    if (isLoading) {
      return 'Checking...';
    }

    if (!health) {
      return 'Unknown';
    }

    return health.overallStatus;
  };

  const getStatusColor = () => {
    if (error) {
      return 'danger';
    }

    if (!health) {
      return 'subtle';
    }

    if (health.overallStatus === 'Healthy') {
      return 'success';
    }

    if (health.overallStatus === 'Degraded') {
      return 'warning';
    }

    return 'danger';
  };

  const getTooltipContent = () => {
    if (error) {
      return 'Unable to connect to system health check';
    }

    if (!health) {
      return 'System status unknown';
    }

    const lines = [
      `Status: ${health.overallStatus}`,
      `Checks: ${health.passedChecks}/${health.totalChecks} passed`,
    ];

    if (health.warningChecks > 0) {
      lines.push(`⚠️ ${health.warningChecks} warning(s)`);
    }

    if (health.failedChecks > 0) {
      lines.push(`❌ ${health.failedChecks} failed`);
    }

    return lines.join('\n');
  };

  const handleViewDetails = () => {
    navigate('/health/system');
  };

  const handleViewProviders = () => {
    navigate('/health/providers');
  };

  const handleRefresh = () => {
    setIsLoading(true);
    fetchHealth();
  };

  return (
    <Menu>
      <MenuTrigger disableButtonEnhancement>
        <Tooltip content={getTooltipContent()} relationship="description">
          <Button
            appearance="subtle"
            size="small"
            icon={getStatusIcon()}
            iconPosition="before"
            style={{
              minWidth: 'auto',
              padding: '4px 8px',
            }}
          >
            <Badge appearance="filled" color={getStatusColor()} size="small">
              {getStatusText()}
            </Badge>
            <ChevronDown20Regular style={{ marginLeft: '4px' }} />
          </Button>
        </Tooltip>
      </MenuTrigger>

      <MenuPopover>
        <MenuList>
          <MenuItem onClick={handleViewDetails}>View System Health</MenuItem>
          <MenuItem onClick={handleViewProviders}>View Provider Status</MenuItem>
          <MenuItem onClick={handleRefresh}>Refresh Status</MenuItem>
        </MenuList>
      </MenuPopover>
    </Menu>
  );
}
