import React, { useEffect, useState, useCallback } from 'react';
import { learningService } from '../../services/learning/learningService';
import type {
  DecisionPattern,
  LearningInsight,
  LearningMaturity,
  InferredPreference,
} from '../../types/learning';
import { InsightCard } from './InsightCard';
import { LearningProgress } from './LearningProgress';
import { PatternList } from './PatternList';
import { PreferenceConfirmation } from './PreferenceConfirmation';

interface LearningDashboardProps {
  profileId: string;
}

export const LearningDashboard: React.FC<LearningDashboardProps> = ({ profileId }) => {
  const [patterns, setPatterns] = useState<DecisionPattern[]>([]);
  const [insights, setInsights] = useState<LearningInsight[]>([]);
  const [maturity, setMaturity] = useState<LearningMaturity | null>(null);
  const [preferences, setPreferences] = useState<InferredPreference[]>([]);
  const [loading, setLoading] = useState(true);
  const [analyzing, setAnalyzing] = useState(false);
  const [activeTab, setActiveTab] = useState<'overview' | 'patterns' | 'insights' | 'preferences'>(
    'overview'
  );
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const [patternsRes, insightsRes, maturityRes, preferencesRes] = await Promise.all([
        learningService.getPatterns(profileId),
        learningService.getInsights(profileId),
        learningService.getMaturityLevel(profileId),
        learningService.getInferredPreferences(profileId),
      ]);

      setPatterns(patternsRes.patterns);
      setInsights(insightsRes.insights);
      setMaturity(maturityRes.maturity);
      setPreferences(preferencesRes.preferences);
    } catch (err) {
      setError('Failed to load learning data');
      console.error('Error loading learning data:', err);
    } finally {
      setLoading(false);
    }
  }, [profileId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleAnalyze = async () => {
    try {
      setAnalyzing(true);
      setError(null);
      await learningService.analyze(profileId);
      await loadData(); // Reload data after analysis
    } catch (err) {
      setError('Failed to analyze patterns');
      console.error('Error analyzing patterns:', err);
    } finally {
      setAnalyzing(false);
    }
  };

  const handleConfirmPreference = async (
    preferenceId: string,
    isCorrect: boolean,
    correctedValue?: unknown
  ) => {
    try {
      await learningService.confirmPreference({
        profileId,
        preferenceId,
        isCorrect,
        correctedValue,
      });
      await loadData(); // Reload to update confirmed status
    } catch (err) {
      console.error('Error confirming preference:', err);
    }
  };

  const handleReset = async () => {
    if (!confirm('Are you sure you want to reset all learning data for this profile?')) {
      return;
    }

    try {
      await learningService.resetLearning(profileId);
      await loadData();
    } catch (err) {
      setError('Failed to reset learning data');
      console.error('Error resetting learning:', err);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-gray-500">Loading learning data...</div>
      </div>
    );
  }

  const unconfirmedPreferences = preferences.filter((p) => !p.isConfirmed);

  return (
    <div className="max-w-6xl mx-auto p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">AI Learning Dashboard</h1>
          <p className="text-sm text-gray-600 mt-1">
            Track how the AI is learning your preferences
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={handleAnalyze}
            disabled={analyzing}
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 text-sm font-medium"
          >
            {analyzing ? 'Analyzing...' : '🔄 Analyze Now'}
          </button>
          <button
            onClick={handleReset}
            className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 text-sm font-medium"
          >
            Reset Learning
          </button>
        </div>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded mb-4">
          {error}
        </div>
      )}

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-6">
        <nav className="-mb-px flex gap-6">
          {['overview', 'patterns', 'insights', 'preferences'].map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab as typeof activeTab)}
              className={`py-2 px-1 border-b-2 font-medium text-sm ${
                activeTab === tab
                  ? 'border-blue-500 text-blue-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
              }`}
            >
              {tab.charAt(0).toUpperCase() + tab.slice(1)}
            </button>
          ))}
        </nav>
      </div>

      {/* Unconfirmed Preferences Banner */}
      {unconfirmedPreferences.length > 0 && (
        <div className="mb-6">
          {unconfirmedPreferences.slice(0, 2).map((pref) => (
            <PreferenceConfirmation
              key={pref.preferenceId}
              preference={pref}
              onConfirm={handleConfirmPreference}
              onDismiss={() => {
                // Just hide it for now
              }}
            />
          ))}
        </div>
      )}

      {/* Tab Content */}
      <div className="space-y-6">
        {activeTab === 'overview' && maturity && (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <LearningProgress maturity={maturity} />
            <div className="space-y-4">
              <div className="bg-white border border-gray-200 rounded-lg p-6">
                <h3 className="text-lg font-semibold mb-4">Quick Stats</h3>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <p className="text-2xl font-bold text-blue-600">{patterns.length}</p>
                    <p className="text-sm text-gray-600">Patterns</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-green-600">{insights.length}</p>
                    <p className="text-sm text-gray-600">Insights</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-purple-600">{preferences.length}</p>
                    <p className="text-sm text-gray-600">Preferences</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-orange-600">
                      {preferences.filter((p) => p.isConfirmed).length}
                    </p>
                    <p className="text-sm text-gray-600">Confirmed</p>
                  </div>
                </div>
              </div>

              {insights.length > 0 && (
                <div className="bg-white border border-gray-200 rounded-lg p-6">
                  <h3 className="text-lg font-semibold mb-4">Recent Insights</h3>
                  <div className="space-y-3">
                    {insights.slice(0, 3).map((insight) => (
                      <InsightCard key={insight.insightId} insight={insight} />
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {activeTab === 'patterns' && (
          <div>
            <h2 className="text-xl font-semibold mb-4">Identified Patterns</h2>
            <PatternList patterns={patterns} />
          </div>
        )}

        {activeTab === 'insights' && (
          <div>
            <h2 className="text-xl font-semibold mb-4">Learning Insights</h2>
            <div className="space-y-3">
              {insights.length > 0 ? (
                insights.map((insight) => <InsightCard key={insight.insightId} insight={insight} />)
              ) : (
                <div className="text-center py-8 text-gray-500">
                  <p>No insights available yet.</p>
                  <p className="text-sm mt-2">Make more decisions to generate insights.</p>
                </div>
              )}
            </div>
          </div>
        )}

        {activeTab === 'preferences' && (
          <div>
            <h2 className="text-xl font-semibold mb-4">Inferred Preferences</h2>
            <div className="space-y-3">
              {preferences.length > 0 ? (
                preferences.map((pref) => (
                  <div
                    key={pref.preferenceId}
                    className="border border-gray-200 rounded-lg p-4 bg-white"
                  >
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <span className="font-medium capitalize">{pref.preferenceName}</span>
                          {pref.isConfirmed && (
                            <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded">
                              ✓ Confirmed
                            </span>
                          )}
                        </div>
                        <p className="text-sm text-gray-700 mt-1">
                          {typeof pref.preferenceValue === 'object'
                            ? JSON.stringify(pref.preferenceValue)
                            : String(pref.preferenceValue)}
                        </p>
                        <div className="mt-2">
                          <span className="text-xs text-gray-500 capitalize">{pref.category}</span>
                          <span className="text-xs text-gray-400 mx-2">•</span>
                          <span className="text-xs text-gray-500">
                            Based on {pref.basedOnDecisions} decisions
                          </span>
                        </div>
                      </div>
                      <div className="text-sm">
                        <span
                          className={`px-2 py-1 rounded ${
                            pref.confidence >= 0.7
                              ? 'bg-green-100 text-green-800'
                              : pref.confidence >= 0.4
                                ? 'bg-yellow-100 text-yellow-800'
                                : 'bg-gray-100 text-gray-800'
                          }`}
                        >
                          {Math.round(pref.confidence * 100)}%
                        </span>
                      </div>
                    </div>
                  </div>
                ))
              ) : (
                <div className="text-center py-8 text-gray-500">
                  <p>No preferences inferred yet.</p>
                  <p className="text-sm mt-2">
                    Make more decisions to help the AI learn your preferences.
                  </p>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
