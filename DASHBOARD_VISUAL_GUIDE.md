# Professional Dashboard - Visual Guide

## Overview
The Professional Dashboard provides an executive-level overview of video projects, system health, and usage analytics. It features a responsive design that adapts from desktop (3-column grid) to mobile (single column).

## Layout Structure

### Hero Section
```
┌─────────────────────────────────────────────────────────────┐
│ Good [morning/afternoon/evening], welcome back!              │
│ Create professional videos with AI-powered automation       │
│                                                              │
│ [Create New Video] (Primary CTA Button)                     │
│                                                              │
│ ┌─────────────┬─────────────┬─────────────┐               │
│ │ Videos Today│ Total Storage│ API Credits │               │
│ │      0      │    0 MB      │    1,000    │               │
│ └─────────────┴─────────────┴─────────────┘               │
└─────────────────────────────────────────────────────────────┘
```

### Main Content Area (2-Column Layout)

#### Left Column - Projects Section
```
┌─────────────────────────────────────────────────────────────┐
│ Recent Projects                              [View All]      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐                    │
│ │ [Play▶]  │ │ [Play▶]  │ │ [Play▶]  │                    │
│ │ Project1 │ │ Project2 │ │ Project3 │                    │
│ │ 5m ago   │ │ 1h ago   │ │ 2d ago   │                    │
│ │ 2:30     │ │ 1:45     │ │ 3:15     │                    │
│ │[Complete]│ │[Process]│ │[Draft]    │                    │
│ │    [⋮]   │ │    [⋮]   │ │    [⋮]   │                    │
│ └──────────┘ └──────────┘ └──────────┘                    │
│                                                              │
│ Quick Start                                                 │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐      │
│ │   📄     │ │   📝     │ │   📋     │ │   ⬆️     │      │
│ │ From     │ │ From     │ │ Batch    │ │ Import   │      │
│ │ Template │ │ Script   │ │ Create   │ │ Project  │      │
│ └──────────┘ └──────────┘ └──────────┘ └──────────┘      │
└─────────────────────────────────────────────────────────────┘
```

#### Right Column - Analytics Widgets
```
┌─────────────────────────────────────────┐
│ API Usage              [⋯Export]        │
│ [Show: API Calls ▼]                    │
│                                         │
│     📊 Line Chart                       │
│     Last 7 days                         │
│     API Calls/Cost over time           │
│                                         │
├─────────────────────────────────────────┤
│ Provider Health                         │
│                                         │
│ ✅ OpenAI          Healthy   120ms     │
│ ✅ ElevenLabs      Healthy   250ms     │
│ ✅ FFmpeg          Healthy    0ms      │
│                                         │
├─────────────────────────────────────────┤
│ Quick Insights                          │
│                                         │
│ Most Used Template    N/A              │
│ Avg Video Duration    0:00             │
│ Peak Usage Hours      N/A              │
│ Favorite Voice        N/A              │
└─────────────────────────────────────────┘
```

### Notification Center (Top Bar)
```
┌─────────────────────────────────────────────────────────────┐
│                                         [🔔 3]  [Results]    │
└─────────────────────────────────────────────────────────────┘

When clicked, shows dropdown:
┌─────────────────────────────────────┐
│ Notifications    [Mark all] [Clear] │
├─────────────────────────────────────┤
│ ✅ Video Ready                [✕]   │
│    Your video is ready              │
│    Just now                         │
├─────────────────────────────────────┤
│ ⚠️  API Limit Approaching     [✕]   │
│    80% of credits used              │
│    5m ago                           │
├─────────────────────────────────────┤
│ ℹ️  New Feature Available     [✕]   │
│    Check out batch creation         │
│    1h ago                           │
└─────────────────────────────────────┘
```

## Component Features

### Project Card
- **16:9 Thumbnail**: Video preview with hover play button overlay
- **Title**: Click to edit, truncated with ellipsis
- **Metadata**: Relative time, duration badge, view count (if shared)
- **Status Badge**: Color-coded (Draft=blue, Processing=yellow, Complete=green, Failed=red)
- **Actions Menu**: Edit, Duplicate, Share, Delete
- **Drag-to-Reorder**: Supports HTML5 drag-and-drop
- **Loading State**: Skeleton loader with pulsing animation
- **Progress Bar**: Shown for processing videos

### Usage Chart (Analytics)
- **Line Graph**: Powered by Recharts
- **Toggle View**: Switch between API Calls and Cost
- **Interactive**: Hover for exact values, responsive tooltips
- **Export**: PNG or CSV download options
- **Time Range**: Last 7 days by default

### Provider Health Widget
- **Status Indicators**:
  - 🟢 Green = Healthy
  - 🟡 Yellow = Degraded
  - 🔴 Red = Down
- **Metrics**: Response time in milliseconds
- **Error Rate**: Shown if > 1%
- **Interactive**: Click to see detailed modal (future implementation)

### Quick Insights
- **Most Used Template**: Track popular templates
- **Avg Video Duration**: Calculate from all completed videos
- **Peak Usage Hours**: Identify busy times
- **Favorite Voice**: Most used TTS voice

## Responsive Behavior

### Desktop (>1024px)
- 2-column layout (Projects | Widgets)
- 3-column project grid
- All features visible

### Tablet (768px - 1024px)
- Single column layout
- Widgets move below projects
- 2-column project grid

### Mobile (<768px)
- Single column layout
- Single project per row
- Simplified widgets
- Touch-optimized interactions

## Empty States

### No Projects
```
┌─────────────────────────────────────┐
│                                     │
│         📹 No projects yet          │
│                                     │
│  Get started by creating your       │
│         first video                 │
│                                     │
│    [Create Your First Video]        │
│                                     │
└─────────────────────────────────────┘
```

### No Notifications
```
┌─────────────────────────────────────┐
│          No notifications           │
└─────────────────────────────────────┘
```

## Interactions

### Project Card Actions
1. **Click Thumbnail**: Opens preview modal
2. **Click Title**: Opens video editor
3. **Click Menu (⋮)**: Shows actions dropdown
   - Edit → Navigate to editor
   - Duplicate → Create copy
   - Share → Open share dialog
   - Delete → Confirm and remove
4. **Drag Card**: Reorder projects (persists order)

### Chart Interactions
1. **Toggle Button**: Switch between API Calls and Cost
2. **Hover Point**: Show exact value tooltip
3. **Export Menu**: Download as PNG or CSV

### Notification Interactions
1. **Click Bell**: Open/close dropdown
2. **Click Notification**: Mark as read and trigger action
3. **Click X**: Dismiss individual notification
4. **Mark All Read**: Mark all as read
5. **Clear All**: Remove all notifications

## Color Scheme (Fluent UI Tokens)

### Status Colors
- **Success**: Green (#107C10)
- **Warning**: Yellow (#F7630C)
- **Error**: Red (#D13438)
- **Info**: Blue (#0078D4)
- **Processing**: Orange (#CA5010)

### UI Elements
- **Background**: Neutral (#FFFFFF light, #1E1E1E dark)
- **Cards**: Elevated with shadow
- **Borders**: Subtle neutral strokes
- **Text**: Primary, Secondary, Tertiary hierarchy

## Accessibility

### Keyboard Navigation
- Tab through interactive elements
- Enter/Space to activate buttons
- Arrow keys for menu navigation
- Escape to close modals/dropdowns

### Screen Readers
- ARIA labels on all interactive elements
- Semantic HTML structure
- Descriptive button text
- Status announcements

## State Persistence

### Zustand Stores
- **Dashboard Store**: Projects, stats, layout preferences
- **Notification Store**: All notifications and read status

### Persisted Data
- Project order
- Layout configuration (future)
- Filter selections (future)
- Notification history

## Performance

### Optimizations
- Lazy loading for large project lists
- Memoized components (React.memo)
- Virtualization for long lists (future)
- Optimized bundle with code splitting

### Bundle Impact
- Dashboard: ~50KB (gzipped)
- Recharts: ~80KB (gzipped)
- Total: +2.4MB to bundle (includes Recharts and dependencies)

## Future Enhancements

### Planned Features
- [ ] Recent briefs quick access
- [ ] Customizable widget layout (drag-and-drop)
- [ ] Saved views (Default, Compact, Analytics)
- [ ] Advanced filtering (status, date, template)
- [ ] Real-time updates via WebSocket
- [ ] Export dashboard as PDF
- [ ] Scheduled reports

### Integration Points
- Connect to actual job API for real project data
- Integrate with analytics service for usage data
- Connect to provider health monitoring
- Sync with user preferences service
