import {
  Card,
  CardHeader,
  Text,
  makeStyles,
  tokens,
  Button,
  Dropdown,
  Option,
  Spinner,
} from '@fluentui/react-components';
import { CalendarRegular } from '@fluentui/react-icons';
import React, { useState, useEffect, useCallback } from 'react';
import { contentPlanningService, ScheduledContent } from '../../services/contentPlanningService';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  controls: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  calendar: {
    display: 'grid',
    gridTemplateColumns: 'repeat(7, 1fr)',
    gap: tokens.spacingHorizontalS,
  },
  dayHeader: {
    textAlign: 'center',
    fontWeight: tokens.fontWeightSemibold,
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
  },
  dayCell: {
    minHeight: '100px',
    padding: tokens.spacingVerticalS,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
      borderColor: tokens.colorBrandStroke1 as string,
    },
  },
  dayNumber: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
    marginBottom: tokens.spacingVerticalXS,
  },
  contentItem: {
    fontSize: tokens.fontSizeBase100,
    padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXXS}`,
    marginBottom: tokens.spacingVerticalXXS,
    borderRadius: tokens.borderRadiusSmall,
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  legend: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    padding: tokens.spacingVerticalM,
  },
  legendItem: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
  },
  legendColor: {
    width: '12px',
    height: '12px',
    borderRadius: tokens.borderRadiusCircular,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground2,
  },
});

const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const platforms = ['All', 'YouTube', 'TikTok', 'Instagram'];

export const ContentCalendarView: React.FC = () => {
  const styles = useStyles();
  const [scheduledContent, setScheduledContent] = useState<ScheduledContent[]>([]);
  const [loading, setLoading] = useState(false);
  const [platform, setPlatform] = useState<string>('All');
  const [currentMonth, setCurrentMonth] = useState(new Date());

  const loadScheduledContent = useCallback(async () => {
    setLoading(true);
    try {
      const startDate = new Date(currentMonth.getFullYear(), currentMonth.getMonth(), 1);
      const endDate = new Date(currentMonth.getFullYear(), currentMonth.getMonth() + 1, 0);

      const response = await contentPlanningService.getScheduledContent(
        startDate.toISOString(),
        endDate.toISOString(),
        platform !== 'All' ? platform : undefined
      );
      setScheduledContent(response.content);
    } catch (error) {
      console.error('Failed to load scheduled content:', error);
    } finally {
      setLoading(false);
    }
  }, [currentMonth, platform]);

  useEffect(() => {
    loadScheduledContent();
  }, [loadScheduledContent]);

  const getDaysInMonth = () => {
    const year = currentMonth.getFullYear();
    const month = currentMonth.getMonth();
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const daysInMonth = lastDay.getDate();
    const startDayOfWeek = firstDay.getDay();

    const days = [];
    // Add empty cells for days before the first of the month
    for (let i = 0; i < startDayOfWeek; i++) {
      days.push(null);
    }
    // Add all days of the month
    for (let day = 1; day <= daysInMonth; day++) {
      days.push(new Date(year, month, day));
    }
    return days;
  };

  const getContentForDay = (date: Date | null) => {
    if (!date) return [];
    return scheduledContent.filter((content) => {
      const contentDate = new Date(content.scheduledDateTime);
      return (
        contentDate.getDate() === date.getDate() &&
        contentDate.getMonth() === date.getMonth() &&
        contentDate.getFullYear() === date.getFullYear()
      );
    });
  };

  const navigateMonth = (direction: number) => {
    setCurrentMonth(new Date(currentMonth.getFullYear(), currentMonth.getMonth() + direction, 1));
  };

  const monthName = currentMonth.toLocaleDateString('en-US', {
    month: 'long',
    year: 'numeric',
  });

  return (
    <div className={styles.container}>
      <Card>
        <CardHeader
          header={<Text weight="semibold">Content Calendar</Text>}
          description="View and manage your scheduled content"
        />
        <div style={{ padding: tokens.spacingVerticalM }}>
          <div className={styles.controls}>
            <Button onClick={() => navigateMonth(-1)}>Previous</Button>
            <Text weight="semibold" size={400}>
              {monthName}
            </Text>
            <Button onClick={() => navigateMonth(1)}>Next</Button>
            <Dropdown
              placeholder="Filter by platform"
              value={platform}
              onOptionSelect={(_e, data) => setPlatform(data.optionValue as string)}
            >
              {platforms.map((p) => (
                <Option key={p} value={p}>
                  {p}
                </Option>
              ))}
            </Dropdown>
          </div>
        </div>
      </Card>

      {loading && (
        <div style={{ textAlign: 'center', padding: tokens.spacingVerticalXXL }}>
          <Spinner label="Loading calendar..." />
        </div>
      )}

      {!loading && (
        <>
          <div className={styles.calendar}>
            {dayNames.map((day) => (
              <div key={day} className={styles.dayHeader}>
                {day}
              </div>
            ))}
            {getDaysInMonth().map((date, index) => {
              const dayContent = date ? getContentForDay(date) : [];
              return (
                <div key={index} className={styles.dayCell}>
                  {date && (
                    <>
                      <div className={styles.dayNumber}>{date.getDate()}</div>
                      {dayContent.map((content) => (
                        <div
                          key={content.id}
                          className={styles.contentItem}
                          title={`${content.title} - ${content.platform}`}
                        >
                          {content.title}
                        </div>
                      ))}
                    </>
                  )}
                </div>
              );
            })}
          </div>

          {scheduledContent.length === 0 && (
            <div className={styles.emptyState}>
              <CalendarRegular
                style={{ fontSize: '48px', marginBottom: tokens.spacingVerticalM }}
              />
              <Text size={400}>No scheduled content for this month.</Text>
            </div>
          )}

          {scheduledContent.length > 0 && (
            <Card>
              <div className={styles.legend}>
                <div className={styles.legendItem}>
                  <div
                    className={styles.legendColor}
                    style={{ backgroundColor: tokens.colorBrandBackground2 }}
                  />
                  <Text size={200}>Scheduled Content</Text>
                </div>
              </div>
            </Card>
          )}
        </>
      )}
    </div>
  );
};
