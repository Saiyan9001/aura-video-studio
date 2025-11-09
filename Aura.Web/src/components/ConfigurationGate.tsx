/**
 * Configuration Gate Component
 *
 * Enforces that required settings are configured before allowing access to features.
 * Redirects users to the Setup Wizard if critical settings are missing or invalid.
 */

import { Spinner, MessageBar, MessageBarBody, Button } from '@fluentui/react-components';
import { Warning24Regular } from '@fluentui/react-icons';
import { useEffect, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { setupApi } from '../services/api/setupApi';
import { hasCompletedFirstRun } from '../services/firstRunService';
import { validateRequiredSettings } from '../services/settingsValidationService';

interface ConfigurationGateProps {
  children: React.ReactNode;
}

/**
 * Routes that don't require configuration to be complete
 */
const ALLOWED_ROUTES = ['/setup', '/settings', '/logs', '/health'];

/**
 * Check if a route is allowed without setup completion
 */
function isRouteAllowed(pathname: string): boolean {
  return ALLOWED_ROUTES.some((route) => pathname.startsWith(route));
}

export function ConfigurationGate({ children }: ConfigurationGateProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const [isChecking, setIsChecking] = useState(true);
  const [settingsValid, setSettingsValid] = useState(true);
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    async function checkConfiguration() {
      // Skip check for allowed routes
      if (isRouteAllowed(location.pathname)) {
        setIsChecking(false);
        setSettingsValid(true);
        return;
      }

      try {
        // Check system setup status from backend (primary check)
        try {
          const systemStatus = await setupApi.getSystemStatus();
          if (!systemStatus.isComplete) {
            // Redirect to setup wizard - system setup not complete
            navigate('/setup', { replace: true });
            return;
          }
        } catch (error) {
          console.warn('Could not check system setup status, falling back to local check:', error);
        }

        // Check if first-run is complete (secondary check for backward compatibility)
        const firstRunComplete = await hasCompletedFirstRun();

        if (!firstRunComplete) {
          // Redirect to setup wizard
          navigate('/setup', { replace: true });
          return;
        }

        // Validate required settings
        const validation = await validateRequiredSettings();

        if (!validation.valid) {
          setSettingsValid(false);
          setValidationError(validation.error || 'Required settings are missing or invalid');
        } else {
          setSettingsValid(true);
          setValidationError(null);
        }
      } catch (error: unknown) {
        console.error('Configuration check failed:', error);
        // On error, allow access but show warning
        setSettingsValid(true);
        setValidationError(null);
      } finally {
        setIsChecking(false);
      }
    }

    checkConfiguration();
  }, [location.pathname, navigate]);

  // Show loading spinner while checking
  if (isChecking) {
    return (
      <div
        style={{
          height: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexDirection: 'column',
          gap: '16px',
        }}
      >
        <Spinner size="large" label="Checking configuration..." />
      </div>
    );
  }

  // Show error banner if settings are invalid
  if (!settingsValid && validationError) {
    return (
      <div style={{ padding: '20px' }}>
        <MessageBar intent="error" icon={<Warning24Regular />}>
          <MessageBarBody>
            <strong>Setup Required:</strong> {validationError}
            <br />
            <Button
              appearance="primary"
              style={{ marginTop: '12px' }}
              onClick={() => navigate('/setup')}
            >
              Go to Setup
            </Button>
          </MessageBarBody>
        </MessageBar>
        {children}
      </div>
    );
  }

  // All checks passed, render children
  return <>{children}</>;
}
