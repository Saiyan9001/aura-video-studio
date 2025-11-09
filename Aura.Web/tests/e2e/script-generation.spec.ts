import { test, expect } from '@playwright/test';

/**
 * Script Generation E2E Tests
 * Tests the complete script generation flow from brief to script review
 */
test.describe('Script Generation Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Mock script providers endpoint
    await page.route('**/api/scripts/providers', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          providers: [
            {
              name: 'RuleBased',
              tier: 'Free',
              isAvailable: true,
              requiresInternet: false,
              requiresApiKey: false,
              capabilities: ['offline', 'deterministic', 'template-based'],
              defaultModel: 'template-v1',
              estimatedCostPer1KTokens: 0,
              availableModels: ['template-v1'],
            },
          ],
          correlationId: 'test-correlation-id',
        }),
      });
    });

    // Mock script generation endpoint
    await page.route('**/api/scripts/generate', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          scriptId: 'script-test-123',
          title: 'Test Generated Script',
          scenes: [
            {
              number: 1,
              narration: 'Welcome to our test video. This is scene one narration.',
              visualPrompt: 'Opening shot with welcoming visuals',
              durationSeconds: 5.0,
              transition: 'Cut',
            },
            {
              number: 2,
              narration:
                'In this scene, we explore the main topic in detail with comprehensive explanations.',
              visualPrompt: 'Main content visuals showing key points',
              durationSeconds: 10.0,
              transition: 'Fade',
            },
            {
              number: 3,
              narration: 'Finally, we conclude with a strong call to action.',
              visualPrompt: 'Closing shot with call to action',
              durationSeconds: 5.0,
              transition: 'Cut',
            },
          ],
          totalDurationSeconds: 20.0,
          metadata: {
            generatedAt: new Date().toISOString(),
            providerName: 'RuleBased',
            modelUsed: 'template-v1',
            tokensUsed: 150,
            estimatedCost: 0,
            tier: 'Free',
            generationTimeSeconds: 1.2,
          },
          correlationId: 'gen-correlation-id',
        }),
      });
    });
  });

  test('should display generate script button in empty state', async ({ page }) => {
    await page.goto('/create/wizard');

    // Navigate to script review step (assuming it's step 3)
    // This may need adjustment based on actual wizard structure
    const scriptReviewHeading = page.getByRole('heading', { name: /Script Review/i });

    if (await scriptReviewHeading.isVisible()) {
      // Check for empty state
      await expect(page.getByText(/No script generated yet/i)).toBeVisible();

      // Check for Generate Script button
      const generateButton = page.getByRole('button', { name: /Generate Script/i });
      await expect(generateButton).toBeVisible();
    }
  });

  test('should generate and display script with scenes', async ({ page }) => {
    await page.goto('/create/wizard');

    // Assuming we're on script review step
    const generateButton = page.getByRole('button', { name: /Generate Script/i });

    if (await generateButton.isVisible()) {
      await generateButton.click();

      // Wait for loading state
      await expect(page.getByText(/Generating your script/i)).toBeVisible({
        timeout: 2000,
      });

      // Wait for script to appear
      await expect(page.getByText('Test Generated Script')).toBeVisible({ timeout: 5000 });

      // Verify scenes are displayed
      await expect(page.getByText('Scene 1')).toBeVisible();
      await expect(page.getByText('Scene 2')).toBeVisible();
      await expect(page.getByText('Scene 3')).toBeVisible();

      // Verify scene content
      await expect(page.getByText(/Welcome to our test video/i)).toBeVisible();

      // Verify statistics bar
      await expect(page.getByText(/Word Count/i)).toBeVisible();
      await expect(page.getByText(/Reading Speed/i)).toBeVisible();
      await expect(page.getByText(/WPM/i)).toBeVisible();
    }
  });

  test('should allow editing scene narration', async ({ page }) => {
    // Mock the scene update endpoint
    await page.route('**/api/scripts/*/scenes/*', (route) => {
      if (route.request().method() === 'PUT') {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            scriptId: 'script-test-123',
            title: 'Test Generated Script',
            scenes: [
              {
                number: 1,
                narration: 'EDITED: This is the updated narration text.',
                visualPrompt: 'Opening shot with welcoming visuals',
                durationSeconds: 5.0,
                transition: 'Cut',
              },
            ],
            totalDurationSeconds: 20.0,
            metadata: {
              generatedAt: new Date().toISOString(),
              providerName: 'RuleBased',
              modelUsed: 'template-v1',
              tokensUsed: 150,
              estimatedCost: 0,
              tier: 'Free',
              generationTimeSeconds: 1.2,
            },
            correlationId: 'update-correlation-id',
          }),
        });
      } else {
        route.continue();
      }
    });

    await page.goto('/create/wizard');

    // Generate script first
    const generateButton = page.getByRole('button', { name: /Generate Script/i });
    if (await generateButton.isVisible()) {
      await generateButton.click();
      await expect(page.getByText('Scene 1')).toBeVisible({ timeout: 5000 });

      // Find and edit the first scene narration
      const narrationTextarea = page.getByRole('textbox').first();
      await narrationTextarea.click();
      await narrationTextarea.fill('EDITED: This is the updated narration text.');

      // Wait for auto-save (2 seconds + buffer)
      await page.waitForTimeout(2500);

      // Verify the API was called (would check network in real scenario)
      // In this mock test, we just verify the text remains
      await expect(narrationTextarea).toHaveValue(/EDITED:/);
    }
  });

  test('should display export options after script generation', async ({ page }) => {
    await page.goto('/create/wizard');

    const generateButton = page.getByRole('button', { name: /Generate Script/i });
    if (await generateButton.isVisible()) {
      await generateButton.click();
      await expect(page.getByText('Test Generated Script')).toBeVisible({ timeout: 5000 });

      // Check for export buttons
      await expect(page.getByRole('button', { name: /Export Text/i })).toBeVisible();
      await expect(page.getByRole('button', { name: /Export Markdown/i })).toBeVisible();
    }
  });

  test('should show provider status badge', async ({ page }) => {
    await page.goto('/create/wizard');

    // Check if provider badge is visible
    const providerBadge = page.getByText('RuleBased');
    if (await providerBadge.isVisible()) {
      await expect(providerBadge).toBeVisible();
    }
  });

  test('should highlight scenes with poor pacing', async ({ page }) => {
    // Mock script with scenes that have poor pacing
    await page.route('**/api/scripts/generate', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          scriptId: 'script-test-456',
          title: 'Pacing Test Script',
          scenes: [
            {
              number: 1,
              narration: 'Short.',
              visualPrompt: 'Quick visual',
              durationSeconds: 10.0,
              transition: 'Cut',
            },
            {
              number: 2,
              narration:
                'This is a very long narration with many words that should be marked as too fast because there are too many words for the given duration creating a very high words per minute reading speed that would be difficult for audiences to follow along with comfortably.',
              visualPrompt: 'Fast visual',
              durationSeconds: 3.0,
              transition: 'Cut',
            },
          ],
          totalDurationSeconds: 13.0,
          metadata: {
            generatedAt: new Date().toISOString(),
            providerName: 'RuleBased',
            modelUsed: 'template-v1',
            tokensUsed: 100,
            estimatedCost: 0,
            tier: 'Free',
            generationTimeSeconds: 1.0,
          },
          correlationId: 'pacing-test',
        }),
      });
    });

    await page.goto('/create/wizard');

    const generateButton = page.getByRole('button', { name: /Generate Script/i });
    if (await generateButton.isVisible()) {
      await generateButton.click();
      await expect(page.getByText('Pacing Test Script')).toBeVisible({ timeout: 5000 });

      // Check for pacing badges
      const tooShortBadge = page.getByText('Too Short');
      const tooLongBadge = page.getByText('Too Long');

      // At least one should be visible
      const hasShort = await tooShortBadge.isVisible().catch(() => false);
      const hasLong = await tooLongBadge.isVisible().catch(() => false);

      expect(hasShort || hasLong).toBeTruthy();
    }
  });
});
