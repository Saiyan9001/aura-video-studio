import {
  makeStyles,
  tokens,
  Input,
  Button,
  Spinner,
  Text,
  Tooltip,
} from '@fluentui/react-components';
import {
  Folder24Regular,
  Checkmark24Filled,
  Dismiss24Filled,
  Info24Regular,
  ArrowClockwise24Regular,
  Dismiss24Regular,
  FolderOpen24Regular,
  Document24Regular,
} from '@fluentui/react-icons';
import { useState, useCallback, useEffect } from 'react';
import type { FC } from 'react';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  inputRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'flex-start',
  },
  input: {
    flex: 1,
  },
  validationRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  validIcon: {
    color: tokens.colorPaletteGreenForeground1,
  },
  invalidIcon: {
    color: tokens.colorPaletteRedForeground1,
  },
  helpText: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  defaultPath: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
    fontFamily: tokens.fontFamilyMonospace,
  },
  versionText: {
    color: tokens.colorNeutralForeground2,
    fontSize: tokens.fontSizeBase200,
  },
});

export type PathSelectorType = 'file' | 'directory';

export interface PathSelectorProps {
  label: string;
  placeholder?: string;
  value: string;
  onChange: (value: string) => void;
  onValidate?: (path: string) => Promise<{ isValid: boolean; message: string; version?: string }>;
  helpText?: string;
  defaultPath?: string;
  fileFilter?: string;
  dependencyId?: string;
  disabled?: boolean;
  autoDetect?: () => Promise<string | null>;
  type?: PathSelectorType;
  accept?: string;
  showClearButton?: boolean;
  showOpenButton?: boolean;
}

export const PathSelector: FC<PathSelectorProps> = ({
  label,
  placeholder,
  value,
  onChange,
  onValidate,
  helpText,
  defaultPath,
  disabled = false,
  autoDetect,
  type = 'file',
  accept = '.exe',
  showClearButton = true,
  showOpenButton = false,
}) => {
  const styles = useStyles();
  const [isValidating, setIsValidating] = useState(false);
  const [isAutoDetecting, setIsAutoDetecting] = useState(false);
  const [validationResult, setValidationResult] = useState<{
    isValid: boolean;
    message: string;
    version?: string;
  } | null>(null);

  const effectivePlaceholder =
    placeholder ||
    (type === 'directory' ? 'Click Browse to select folder' : 'Click Browse to select file');

  const handleValidate = useCallback(
    async (pathToValidate: string) => {
      if (!pathToValidate.trim() || !onValidate) {
        setValidationResult(null);
        return;
      }

      setIsValidating(true);
      try {
        const result = await onValidate(pathToValidate);
        setValidationResult(result);
      } catch (error: unknown) {
        const errorMessage = error instanceof Error ? error.message : 'Validation failed';
        setValidationResult({
          isValid: false,
          message: errorMessage,
        });
      } finally {
        setIsValidating(false);
      }
    },
    [onValidate]
  );

  useEffect(() => {
    if (value && onValidate) {
      const timeoutId = setTimeout(() => {
        handleValidate(value);
      }, 500);

      return () => clearTimeout(timeoutId);
    } else {
      setValidationResult(null);
    }
  }, [value, onValidate, handleValidate]);

  const handleBrowse = useCallback(() => {
    const input = document.createElement('input');
    input.type = 'file';

    if (type === 'directory') {
      input.setAttribute('webkitdirectory', '');
      input.setAttribute('directory', '');
    } else {
      input.accept = accept;
    }

    input.onchange = (e) => {
      const target = e.target as HTMLInputElement;
      const file = target.files?.[0];
      if (file) {
        const path = (file as unknown as { path?: string }).path;
        if (path) {
          onChange(path);
        } else if (file.webkitRelativePath && type === 'directory') {
          const folderPath = file.webkitRelativePath.split('/')[0];
          onChange(folderPath);
        } else if (file.name) {
          onChange(file.name);
        }
      }
    };

    input.click();
  }, [onChange, type, accept]);

  const handleClear = useCallback(() => {
    onChange('');
    setValidationResult(null);
  }, [onChange]);

  const handleOpenInExplorer = useCallback(() => {
    if (!value) return;

    window.open(`file://${value}`, '_blank');
  }, [value]);

  const handleAutoDetect = useCallback(async () => {
    if (!autoDetect) return;

    setIsAutoDetecting(true);
    try {
      const detectedPath = await autoDetect();
      if (detectedPath) {
        onChange(detectedPath);
      }
    } catch (error: unknown) {
      console.error('Auto-detect failed:', error);
    } finally {
      setIsAutoDetecting(false);
    }
  }, [autoDetect, onChange]);

  return (
    <div className={styles.container}>
      <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
        <Text weight="semibold">{label}</Text>
        {helpText && (
          <Tooltip content={helpText} relationship="description">
            <Info24Regular style={{ cursor: 'help', color: tokens.colorNeutralForeground3 }} />
          </Tooltip>
        )}
      </div>

      {defaultPath && <Text className={styles.defaultPath}>Default: {defaultPath}</Text>}

      <div className={styles.inputRow}>
        <Input
          className={styles.input}
          placeholder={effectivePlaceholder}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled || isValidating}
          contentBefore={type === 'directory' ? <Folder24Regular /> : <Document24Regular />}
        />
        <Button
          appearance="secondary"
          onClick={handleBrowse}
          disabled={disabled || isValidating}
          icon={type === 'directory' ? <Folder24Regular /> : <Document24Regular />}
          title={type === 'directory' ? 'Browse for folder' : 'Browse for file'}
        >
          Browse...
        </Button>
        {showClearButton && value && (
          <Button
            appearance="subtle"
            onClick={handleClear}
            disabled={disabled || isValidating}
            icon={<Dismiss24Regular />}
            title="Clear path"
          />
        )}
        {showOpenButton && value && validationResult?.isValid && (
          <Button
            appearance="subtle"
            onClick={handleOpenInExplorer}
            disabled={disabled}
            icon={<FolderOpen24Regular />}
            title="Open in file explorer"
          />
        )}
        {autoDetect && (
          <Button
            appearance="secondary"
            onClick={handleAutoDetect}
            disabled={disabled || isAutoDetecting}
            icon={isAutoDetecting ? <Spinner size="tiny" /> : <ArrowClockwise24Regular />}
            title="Automatically detect installation"
          >
            {isAutoDetecting ? 'Detecting...' : 'Auto-Detect'}
          </Button>
        )}
      </div>

      {isValidating && (
        <div className={styles.validationRow}>
          <Spinner size="tiny" />
          <Text size={200}>Validating path...</Text>
        </div>
      )}

      {!isValidating && validationResult && (
        <div className={styles.validationRow}>
          {validationResult.isValid ? (
            <Checkmark24Filled className={styles.validIcon} />
          ) : (
            <Dismiss24Filled className={styles.invalidIcon} />
          )}
          <Text
            size={200}
            style={{
              color: validationResult.isValid
                ? tokens.colorPaletteGreenForeground1
                : tokens.colorPaletteRedForeground1,
            }}
          >
            {validationResult.message}
          </Text>
          {validationResult.isValid && validationResult.version && (
            <Text className={styles.versionText}>({validationResult.version})</Text>
          )}
        </div>
      )}
    </div>
  );
};
