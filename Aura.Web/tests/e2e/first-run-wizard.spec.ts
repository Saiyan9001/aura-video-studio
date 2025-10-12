import { test, expect } from '@playwright/test';

test.describe('First-Run Wizard E2E', () => {
  test.beforeEach(async ({ page }) => {
    // Clear localStorage to simulate first run
    await page.goto('/');
    await page.evaluate(() => localStorage.removeItem('hasSeenOnboarding'));
  });

  test('should complete wizard flow with Free-Only mode and successful validation', async ({ page }) => {
    // Mock hardware probe API
    await page.route('**/api/hardware/probe', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          gpu: 'Intel UHD Graphics',
          vramGB: 2,
          enableLocalDiffusion: false,
        }),
      });
    });

    // Mock successful preflight check for Free-Only
    await page.route('**/api/preflight?profile=Free-Only*', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'X-Correlation-Id': 'test-correlation-123' },
        body: JSON.stringify({
          ok: true,
          stages: [
            {
              stage: 'Script',
              status: 'pass',
              provider: 'RuleBased',
              message: 'Rule-based script generation available',
            },
            {
              stage: 'TTS',
              status: 'pass',
              provider: 'Windows TTS',
              message: 'Windows Speech Synthesis available',
            },
            {
              stage: 'Visuals',
              status: 'pass',
              provider: 'Stock',
              message: 'Stock images available',
            },
          ],
        }),
      });
    });

    // Navigate to first-run wizard
    await page.goto('/onboarding');

    // Step 0: Mode Selection
    await expect(page.getByRole('heading', { name: 'First-Run Setup' })).toBeVisible();
    await expect(page.getByText('Welcome to Aura Video Studio!')).toBeVisible();

    // Select Free-Only mode
    const freeCard = page.locator('text=Free-Only Mode').locator('..');
    await freeCard.click();
    
    // Click Next
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 1: Hardware Detection
    await expect(page.getByRole('heading', { name: 'Hardware Detection' })).toBeVisible();
    await page.getByRole('button', { name: 'Next' }).click();

    // Should detect hardware automatically
    await expect(page.getByText(/Intel UHD Graphics/)).toBeVisible({ timeout: 5000 });
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 2: Install Required Components
    await expect(page.getByRole('heading', { name: 'Install Required Components' })).toBeVisible();
    
    // FFmpeg is required - verify it's listed
    await expect(page.getByText('FFmpeg (Video encoding)')).toBeVisible();
    await expect(page.getByText('Required')).toBeVisible();

    // Click Next to continue
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 3: Validation & Demo
    await expect(page.getByRole('heading', { name: 'Validation & Demo' })).toBeVisible();
    
    // Button should say "Validate"
    const validateButton = page.getByRole('button', { name: 'Validate' });
    await expect(validateButton).toBeVisible();
    await expect(validateButton).toBeEnabled();

    // Click Validate
    await validateButton.click();

    // Should show "Validating..." state
    await expect(page.getByText('Running preflight checks...')).toBeVisible();

    // After validation succeeds, should show success state
    await expect(page.getByText('All Set!')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Your system is ready to create amazing videos')).toBeVisible();

    // Should show completion buttons
    await expect(page.getByRole('button', { name: 'Create My First Video' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Go to Settings' })).toBeVisible();

    // Click to complete onboarding
    await page.getByRole('button', { name: 'Create My First Video' }).click();

    // Should navigate to create page
    await expect(page).toHaveURL(/\/create/);
  });

  test('should show fix actions when validation fails', async ({ page }) => {
    // Mock hardware probe API
    await page.route('**/api/hardware/probe', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          gpu: 'NVIDIA RTX 3080',
          vramGB: 10,
          enableLocalDiffusion: true,
        }),
      });
    });

    // Mock failed preflight check for Pro mode
    await page.route('**/api/preflight?profile=Pro-Max*', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'X-Correlation-Id': 'test-correlation-456' },
        body: JSON.stringify({
          ok: false,
          stages: [
            {
              stage: 'Script',
              status: 'fail',
              provider: 'OpenAI',
              message: 'API key not configured',
              hint: 'Configure your OpenAI API key in Settings',
              suggestions: [
                'Get API key from https://platform.openai.com/api-keys',
                'Add key in Settings → API Keys → OpenAI',
              ],
              fixActions: [
                {
                  type: 'OpenSettings',
                  label: 'Add API Key',
                  parameter: 'api-keys',
                  description: 'Open Settings to configure OpenAI API key',
                },
                {
                  type: 'Help',
                  label: 'Get API Key',
                  parameter: 'https://platform.openai.com/api-keys',
                  description: 'Open OpenAI website to sign up and get API key',
                },
              ],
            },
            {
              stage: 'TTS',
              status: 'pass',
              provider: 'Windows TTS',
              message: 'Using Windows Speech Synthesis',
            },
            {
              stage: 'Visuals',
              status: 'pass',
              provider: 'Stock',
              message: 'Using stock images',
            },
          ],
        }),
      });
    });

    // Navigate to first-run wizard
    await page.goto('/onboarding');

    // Step 0: Select Pro mode
    const proCard = page.locator('text=Pro Mode').locator('..');
    await proCard.click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 1: Hardware Detection
    await page.getByRole('button', { name: 'Next' }).click();
    await expect(page.getByText(/NVIDIA RTX 3080/)).toBeVisible({ timeout: 5000 });
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 2: Components
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 3: Validation
    await page.getByRole('button', { name: 'Validate' }).click();

    // Should show validation failed state
    await expect(page.getByText('Validation Failed')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('API key not configured')).toBeVisible();

    // Should show hint
    await expect(page.getByText(/Configure your OpenAI API key in Settings/)).toBeVisible();

    // Should show suggestions
    await expect(page.getByText(/Get API key from https:\/\/platform.openai.com/)).toBeVisible();

    // Should show fix action buttons
    await expect(page.getByRole('button', { name: 'Add API Key' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Get API Key' })).toBeVisible();

    // Button should say "Fix Issues" or "Validate" to retry
    const button = page.getByRole('button', { name: /Validate/i }).or(page.getByRole('button', { name: /Fix Issues/i }));
    await expect(button).toBeVisible();
  });

  test('should allow user to go back and change mode', async ({ page }) => {
    await page.goto('/onboarding');

    // Select Free mode
    const freeCard = page.locator('text=Free-Only Mode').locator('..');
    await freeCard.click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Go to step 2
    await page.getByRole('button', { name: 'Next' }).click();

    // Go back
    const backButton = page.getByRole('button', { name: 'Back' });
    await expect(backButton).toBeVisible();
    await backButton.click();

    // Should be on step 1
    await expect(page.getByRole('heading', { name: 'Hardware Detection' })).toBeVisible();

    // Go back again
    await backButton.click();

    // Should be on step 0
    await expect(page.getByText('Welcome to Aura Video Studio!')).toBeVisible();

    // Change to Local mode
    const localCard = page.locator('text=Local Mode').locator('..');
    await localCard.click();

    // Continue forward
    await page.getByRole('button', { name: 'Next' }).click();
  });

  test('should disable buttons during validation', async ({ page }) => {
    // Mock slow validation
    await page.route('**/api/preflight?profile=Free-Only*', async (route) => {
      await new Promise(resolve => setTimeout(resolve, 2000));
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ok: true,
          stages: [
            { stage: 'Script', status: 'pass', provider: 'RuleBased', message: 'OK' },
            { stage: 'TTS', status: 'pass', provider: 'Windows', message: 'OK' },
            { stage: 'Visuals', status: 'pass', provider: 'Stock', message: 'OK' },
          ],
        }),
      });
    });

    await page.goto('/onboarding');

    // Navigate to validation step
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Click Validate
    await page.getByRole('button', { name: 'Validate' }).click();

    // Buttons should be disabled during validation
    await expect(page.getByText('Validating…')).toBeVisible();
    
    const validateButton = page.getByRole('button', { name: /Validating/ });
    await expect(validateButton).toBeDisabled();

    const backButton = page.getByRole('button', { name: 'Back' });
    await expect(backButton).toBeDisabled();
  });

  test('should allow skipping setup', async ({ page }) => {
    await page.goto('/onboarding');

    // Should show Skip Setup button
    const skipButton = page.getByRole('button', { name: 'Skip Setup' });
    await expect(skipButton).toBeVisible();
    await skipButton.click();

    // Should navigate to home page
    await expect(page).toHaveURL('/');
  });

  test('should show step progress indicator', async ({ page }) => {
    await page.goto('/onboarding');

    // Should show 4 step indicators
    const steps = page.locator('[class*="step"]').filter({ hasNot: page.locator('text') });
    await expect(steps).toHaveCount(4); // May vary based on implementation

    // First step should be active
    // Continue through steps and verify indicator updates
    await page.getByRole('button', { name: 'Next' }).click();
    
    // Step 2 should be active now
    await page.getByRole('button', { name: 'Next' }).click();
  });

  test('should not show wizard if already completed', async ({ page }) => {
    // Set hasSeenOnboarding flag
    await page.goto('/');
    await page.evaluate(() => localStorage.setItem('hasSeenOnboarding', 'true'));

    // Navigate to onboarding
    await page.goto('/onboarding');

    // Should redirect to home
    await expect(page).toHaveURL('/');
  });

  test('should allow using existing FFmpeg installation', async ({ page }) => {
    // Mock hardware probe API
    await page.route('**/api/hardware/probe', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          gpu: 'Intel UHD Graphics',
          vramGB: 2,
          enableLocalDiffusion: false,
        }),
      });
    });

    // Mock successful preflight check
    await page.route('**/api/preflight?profile=Free-Only*', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'X-Correlation-Id': 'test-correlation-789' },
        body: JSON.stringify({
          ok: true,
          stages: [
            { stage: 'Script', status: 'pass', provider: 'RuleBased', message: 'OK' },
            { stage: 'TTS', status: 'pass', provider: 'Windows TTS', message: 'OK' },
            { stage: 'Visuals', status: 'pass', provider: 'Stock', message: 'OK' },
          ],
        }),
      });
    });

    await page.goto('/onboarding');

    // Step 0: Select Free-Only mode
    const freeCard = page.locator('text=Free-Only Mode').locator('..');
    await freeCard.click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 1: Hardware Detection
    await page.getByRole('button', { name: 'Next' }).click();
    await expect(page.getByText(/Intel UHD Graphics/)).toBeVisible({ timeout: 5000 });
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 2: Install Required Components
    await expect(page.getByRole('heading', { name: 'Install Required Components' })).toBeVisible();
    
    // Should see FFmpeg with "Use Existing" button
    await expect(page.getByText('FFmpeg (Video encoding)')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Use Existing' })).toBeVisible();

    // Click "Use Existing" for FFmpeg
    await page.getByRole('button', { name: 'Use Existing' }).first().click();

    // Dialog should appear
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText('Use Existing Installation')).toBeVisible();

    // Enter a path
    const pathInput = page.getByPlaceholder(/e.g., C:/);
    await pathInput.fill('C:\\Tools\\ffmpeg');

    // Confirm
    await page.getByRole('button', { name: 'Use This Path' }).click();

    // Dialog should close and FFmpeg should be marked as installed
    await expect(page.getByRole('dialog')).not.toBeVisible();
    await expect(page.getByText('C:\\Tools\\ffmpeg')).toBeVisible();

    // Continue to validation
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Validate' }).click();

    // Should show success
    await expect(page.getByText('All Set!')).toBeVisible({ timeout: 10000 });

    // Should show "Where are my files?" section
    await expect(page.getByText('Where are my files?')).toBeVisible();
    await expect(page.getByText('C:\\Tools\\ffmpeg')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Open Folder' })).toBeVisible();
  });

  test('should allow skipping optional components', async ({ page }) => {
    await page.goto('/onboarding');

    // Navigate to components step
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 2: Components
    await expect(page.getByRole('heading', { name: 'Install Required Components' })).toBeVisible();

    // Optional items should have "Skip for now" button
    const skipButtons = page.getByRole('button', { name: 'Skip for now' });
    const count = await skipButtons.count();
    expect(count).toBeGreaterThan(0);

    // Skip Ollama (optional)
    const ollamaCard = page.locator('text=Ollama').locator('..');
    await ollamaCard.getByRole('button', { name: 'Skip for now' }).click();

    // Should show "Skipped" badge
    await expect(ollamaCard.getByText('Skipped')).toBeVisible();

    // Required item (FFmpeg) should NOT have skip button
    const ffmpegCard = page.locator('text=FFmpeg').locator('..');
    await expect(ffmpegCard.getByRole('button', { name: 'Skip for now' })).not.toBeVisible();
  });
});
