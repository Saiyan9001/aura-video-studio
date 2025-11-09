import {
  Button,
  Card,
  Slider,
  Select,
  Textarea,
  Text,
  Label,
  Spinner,
} from '@fluentui/react-components';
import {
  ArrowRotateClockwise24Regular,
  Sparkle24Regular,
  Star24Regular,
} from '@fluentui/react-icons';
import React, { useState } from 'react';

interface Scene {
  number: number;
  narration: string;
  visualPrompt: string;
  duration: number;
}

interface Script {
  title: string;
  scenes: Scene[];
  totalDuration: number;
}

interface ScriptCustomizationPanelProps {
  script: Script;
  onScriptUpdate: (script: Script) => void;
  onImproveScript: (goal: string) => Promise<void>;
  onOptimizeHook: () => Promise<void>;
  onRegenerateScene: (sceneNumber: number, goal: string) => Promise<void>;
}

/**
 * Script Customization Panel - Advanced editing interface for scripts
 * Provides tone adjustment, pacing control, complexity settings, and style presets
 */
export const ScriptCustomizationPanel: React.FC<ScriptCustomizationPanelProps> = ({
  script,
  onScriptUpdate,
  onImproveScript,
  onOptimizeHook,
  onRegenerateScene,
}) => {
  const [selectedScene, setSelectedScene] = useState<number | null>(null);
  const [editingNarration, setEditingNarration] = useState('');
  const [improvementGoal, setImprovementGoal] = useState('');
  const [isProcessing, setIsProcessing] = useState(false);

  // Customization controls
  const [formalityLevel, setFormalityLevel] = useState(50);
  const [pacingSpeed, setPacingSpeed] = useState(50);
  const [complexityLevel, setComplexityLevel] = useState(50);
  const [stylePreset, setStylePreset] = useState('conversational');

  const stylePresets = [
    { key: 'documentary', text: 'Documentary' },
    { key: 'vlog', text: 'Vlog / Personal' },
    { key: 'tutorial', text: 'Tutorial / How-To' },
    { key: 'conversational', text: 'Conversational' },
    { key: 'professional', text: 'Professional' },
    { key: 'energetic', text: 'Energetic / Dynamic' },
    { key: 'storytelling', text: 'Storytelling' },
  ];

  const handleSceneClick = (sceneNumber: number) => {
    setSelectedScene(sceneNumber);
    const scene = script.scenes.find((s) => s.number === sceneNumber);
    if (scene) {
      setEditingNarration(scene.narration);
    }
  };

  const handleSaveScene = () => {
    if (selectedScene === null) return;

    const updatedScenes = script.scenes.map((scene) =>
      scene.number === selectedScene ? { ...scene, narration: editingNarration } : scene
    );

    onScriptUpdate({ ...script, scenes: updatedScenes });
    setSelectedScene(null);
  };

  const handleImproveScript = async () => {
    if (!improvementGoal) return;

    setIsProcessing(true);
    try {
      await onImproveScript(improvementGoal);
      setImprovementGoal('');
    } finally {
      setIsProcessing(false);
    }
  };

  const handleOptimizeHook = async () => {
    setIsProcessing(true);
    try {
      await onOptimizeHook();
    } finally {
      setIsProcessing(false);
    }
  };

  const handleRegenerateScene = async () => {
    if (selectedScene === null) return;

    setIsProcessing(true);
    try {
      await onRegenerateScene(selectedScene, improvementGoal || 'Improve overall quality');
      setSelectedScene(null);
    } finally {
      setIsProcessing(false);
    }
  };

  const getFormalityDescription = () => {
    if (formalityLevel < 33) return 'Casual & Friendly';
    if (formalityLevel < 67) return 'Conversational';
    return 'Formal & Professional';
  };

  const getPacingDescription = () => {
    if (pacingSpeed < 33) return 'Slow & Deliberate';
    if (pacingSpeed < 67) return 'Medium Pace';
    return 'Fast & Dynamic';
  };

  const getComplexityDescription = () => {
    if (complexityLevel < 33) return 'Simple & Clear';
    if (complexityLevel < 67) return 'Moderate Detail';
    return 'Detailed & In-Depth';
  };

  return (
    <div className="script-customization-panel">
      <Card className="customization-controls">
        <h3>Script Customization</h3>

        {/* Style Preset Selector */}
        <div className="control-group">
          <Label>Style Preset</Label>
          <Select value={stylePreset} onChange={(_, data) => setStylePreset(data.value)}>
            {stylePresets.map((preset) => (
              <option key={preset.key} value={preset.key}>
                {preset.text}
              </option>
            ))}
          </Select>
        </div>

        {/* Tone Adjustment Slider */}
        <div className="control-group">
          <Label>
            Tone: {getFormalityDescription()}
            <Text size={200} className="slider-subtitle">
              Casual ↔ Formal
            </Text>
          </Label>
          <Slider
            min={0}
            max={100}
            value={formalityLevel}
            onChange={(_, data) => setFormalityLevel(data.value)}
          />
        </div>

        {/* Pacing Control */}
        <div className="control-group">
          <Label>
            Pacing: {getPacingDescription()}
            <Text size={200} className="slider-subtitle">
              Slow ↔ Fast
            </Text>
          </Label>
          <Slider
            min={0}
            max={100}
            value={pacingSpeed}
            onChange={(_, data) => setPacingSpeed(data.value)}
          />
        </div>

        {/* Complexity Setting */}
        <div className="control-group">
          <Label>
            Complexity: {getComplexityDescription()}
            <Text size={200} className="slider-subtitle">
              Simple ↔ Detailed
            </Text>
          </Label>
          <Slider
            min={0}
            max={100}
            value={complexityLevel}
            onChange={(_, data) => setComplexityLevel(data.value)}
          />
        </div>

        {/* Quick Actions */}
        <div className="quick-actions">
          <Button
            appearance="primary"
            icon={<Sparkle24Regular />}
            onClick={handleOptimizeHook}
            disabled={isProcessing}
          >
            Optimize Hook
          </Button>

          <Button
            appearance="secondary"
            icon={<ArrowRotateClockwise24Regular />}
            disabled={isProcessing}
          >
            Generate Variations
          </Button>
        </div>
      </Card>

      {/* Scene List */}
      <Card className="scene-list">
        <h3>Scenes ({script.scenes.length})</h3>

        {script.scenes.map((scene) => (
          <Card
            key={scene.number}
            className={`scene-card ${selectedScene === scene.number ? 'selected' : ''}`}
            onClick={() => handleSceneClick(scene.number)}
          >
            <div className="scene-header">
              <Text weight="semibold">Scene {scene.number}</Text>
              <Text size={200}>{scene.duration}s</Text>
            </div>

            <Text className="scene-narration">
              {scene.narration.substring(0, 100)}
              {scene.narration.length > 100 ? '...' : ''}
            </Text>

            <Text size={200} className="scene-visual">
              Visual: {scene.visualPrompt}
            </Text>
          </Card>
        ))}
      </Card>

      {/* Scene Editor */}
      {selectedScene !== null && (
        <Card className="scene-editor">
          <h3>Edit Scene {selectedScene}</h3>

          <Label>Narration</Label>
          <Textarea
            value={editingNarration}
            onChange={(_, data) => setEditingNarration(data.value)}
            rows={6}
            resize="vertical"
          />

          <div className="editor-actions">
            <Button appearance="primary" onClick={handleSaveScene} disabled={isProcessing}>
              Save Changes
            </Button>

            <Button
              appearance="secondary"
              icon={<ArrowRotateClockwise24Regular />}
              onClick={handleRegenerateScene}
              disabled={isProcessing}
            >
              Regenerate Scene
            </Button>

            <Button onClick={() => setSelectedScene(null)} disabled={isProcessing}>
              Cancel
            </Button>
          </div>
        </Card>
      )}

      {/* Improvement Panel */}
      <Card className="improvement-panel">
        <h3>Script Improvement</h3>

        <Label>What would you like to improve?</Label>
        <Textarea
          value={improvementGoal}
          onChange={(_, data) => setImprovementGoal(data.value)}
          placeholder="E.g., 'Make the opening more engaging' or 'Add more specific examples'"
          rows={3}
        />

        <Button
          appearance="primary"
          icon={<Star24Regular />}
          onClick={handleImproveScript}
          disabled={!improvementGoal || isProcessing}
        >
          {isProcessing ? (
            <>
              <Spinner size="tiny" /> Improving...
            </>
          ) : (
            'Improve Script'
          )}
        </Button>
      </Card>

      <style>{`
        .script-customization-panel {
          display: grid;
          grid-template-columns: 300px 1fr 400px;
          gap: 1rem;
          padding: 1rem;
        }

        .customization-controls,
        .scene-list,
        .scene-editor,
        .improvement-panel {
          padding: 1.5rem;
        }

        .control-group {
          margin-bottom: 1.5rem;
        }

        .slider-subtitle {
          display: block;
          color: var(--colorNeutralForeground3);
          margin-top: 0.25rem;
        }

        .quick-actions {
          display: flex;
          gap: 0.5rem;
          flex-direction: column;
          margin-top: 1rem;
        }

        .scene-card {
          margin-bottom: 0.5rem;
          padding: 1rem;
          cursor: pointer;
          transition: all 0.2s;
        }

        .scene-card:hover {
          background-color: var(--colorNeutralBackground1Hover);
        }

        .scene-card.selected {
          border: 2px solid var(--colorBrandBackground);
          background-color: var(--colorNeutralBackground1Selected);
        }

        .scene-header {
          display: flex;
          justify-content: space-between;
          margin-bottom: 0.5rem;
        }

        .scene-narration {
          display: block;
          margin-bottom: 0.5rem;
          line-height: 1.4;
        }

        .scene-visual {
          display: block;
          color: var(--colorNeutralForeground3);
          font-style: italic;
        }

        .editor-actions {
          display: flex;
          gap: 0.5rem;
          margin-top: 1rem;
        }

        @media (max-width: 1200px) {
          .script-customization-panel {
            grid-template-columns: 1fr;
          }
        }
      `}</style>
    </div>
  );
};

export default ScriptCustomizationPanel;
