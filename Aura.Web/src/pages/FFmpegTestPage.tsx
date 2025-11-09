import { makeStyles, tokens, Title1 } from '@fluentui/react-components';
import { FFmpegSetup } from '../components/FirstRun/FFmpegSetup';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    padding: tokens.spacingVerticalXXL,
    maxWidth: '900px',
    margin: '0 auto',
    gap: tokens.spacingVerticalL,
  },
});

export function FFmpegTestPage() {
  const styles = useStyles();

  return (
    <div className={styles.container}>
      <Title1>FFmpeg Setup Test Page</Title1>
      <FFmpegSetup onStatusChange={(installed) => console.log('FFmpeg installed:', installed)} />
    </div>
  );
}

export default FFmpegTestPage;
