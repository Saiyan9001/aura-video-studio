import {
  makeStyles,
  tokens,
  Button,
  Popover,
  PopoverTrigger,
  PopoverSurface,
} from '@fluentui/react-components';
import { Add24Regular, Dismiss24Regular } from '@fluentui/react-icons';
import { useState } from 'react';
import { QuickAccessPanel } from './QuickAccessPanel';

const useStyles = makeStyles({
  fab: {
    position: 'fixed',
    bottom: '80px',
    right: tokens.spacingHorizontalL,
    width: '56px',
    height: '56px',
    borderRadius: '50%',
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    boxShadow: tokens.shadow16,
    display: 'none',
    zIndex: 999,
    transition: 'transform 0.2s, box-shadow 0.2s',
    ':hover': {
      transform: 'scale(1.1)',
      boxShadow: tokens.shadow28,
    },
    ':active': {
      transform: 'scale(0.95)',
    },
    '@media (max-width: 768px)': {
      display: 'flex',
    },
  },
  fabOpen: {
    backgroundColor: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
  },
  popoverSurface: {
    padding: 0,
    maxHeight: 'calc(100vh - 150px)',
    overflowY: 'auto',
    '@media (max-width: 768px)': {
      width: 'calc(100vw - 32px)',
      maxWidth: '320px',
    },
  },
});

export function MobileFAB() {
  const styles = useStyles();
  const [isOpen, setIsOpen] = useState(false);

  return (
    <Popover open={isOpen} onOpenChange={(_, data) => setIsOpen(data.open)} positioning="above-end">
      <PopoverTrigger disableButtonEnhancement>
        <Button
          appearance="primary"
          shape="circular"
          className={isOpen ? `${styles.fab} ${styles.fabOpen}` : styles.fab}
          icon={isOpen ? <Dismiss24Regular /> : <Add24Regular />}
          aria-label="Quick actions"
        />
      </PopoverTrigger>
      <PopoverSurface className={styles.popoverSurface}>
        <QuickAccessPanel onClose={() => setIsOpen(false)} />
      </PopoverSurface>
    </Popover>
  );
}
