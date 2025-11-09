import { test, expect } from '@playwright/test';

test.describe('Onboarding with Path Pickers', () => {
  test.beforeEach(async ({ page }) => {
    // Clear localStorage to simulate first run
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.removeItem('hasSeenOnboarding');
      localStorage.removeItem('hasCompletedFirstRun');
    });
  });

  test('should complete onboarding using existing FFmpeg path', async ({ page }) => {
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

    // Mock engine attach API
    let attachCalled = false;
    await page.route('**/api/engines/attach', (route) => {
      attachCalled = true;
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          message: 'Engine attached successfully',
        }),
      });
    });

    // Mock successful preflight check
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

    // Mock engine instances API for file locations
    await page.route('**/api/engines/instances', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          instances: [
            {
              engineId: 'ffmpeg',
              engineName: 'FFmpeg',
              installPath: 'C:\\Tools\\ffmpeg',
              port: null,
            },
          ],
        }),
      });
    });

    // Navigate to first-run wizard
    await page.goto('/setup');

    // Step 0: Mode Selection
    await expect(page.getByRole('heading', { name: 'First-Run Setup' })).toBeVisible();

    // Select Free-Only mode
    const freeCard = page.locator('text=Free-Only Mode').locator('..');
    await freeCard.click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 1: Hardware Detection
    await page.getByRole('button', { name: 'Next' }).click();
    await expect(page.getByText(/Intel UHD Graphics/)).toBeVisible({ timeout: 5000 });
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 2: Install Required Components
    await expect(page.getByRole('heading', { name: 'Install Required Components' })).toBeVisible();
    await expect(page.getByText('FFmpeg (Video encoding)')).toBeVisible();

    // Click "Use Existing" button for FFmpeg
    const useExistingButton = page.getByRole('button', { name: /use existing/i }).first();
    await useExistingButton.click();

    // Dialog should open
    await expect(page.getByText(/Use Existing FFmpeg/i)).toBeVisible();

    // Fill in existing FFmpeg path
    const installPathInput = page.getByLabel(/install path/i);
    await installPathInput.fill('C:\\Tools\\ffmpeg');

    const executablePathInput = page.getByLabel(/executable path/i);
    await executablePathInput.fill('C:\\Tools\\ffmpeg\\bin\\ffmpeg.exe');

    // Click Attach & Validate
    const attachButton = page.getByRole('button', { name: /attach & validate/i });
    await attachButton.click();

    // Wait for dialog to close and item to be marked as installed
    await expect(page.getByText(/Use Existing FFmpeg/i)).not.toBeVisible({ timeout: 5000 });

    // Verify attach was called
    expect(attachCalled).toBe(true);

    // Continue to validation step
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 3: Validation
    await expect(page.getByRole('heading', { name: 'Validation & Demo' })).toBeVisible();
    await page.getByRole('button', { name: 'Validate' }).click();

    // Should show success
    await expect(page.getByText('All Set!')).toBeVisible({ timeout: 10000 });

    // Should show file locations summary
    await expect(page.getByText(/Where are my files/i)).toBeVisible();
    await expect(page.getByText('FFmpeg')).toBeVisible();
    await expect(page.getByText(/C:\\Tools\\ffmpeg/)).toBeVisible();

    // Should have Open Folder button
    await expect(page.getByRole('button', { name: /open folder/i })).toBeVisible();
  });

  test('should complete onboarding using existing SD install', async ({ page }) => {
    // Mock hardware probe API with GPU support
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

    // Mock engine attach API
    await page.route('**/api/engines/attach', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          message: 'Engine attached successfully',
        }),
      });
    });

    // Mock successful preflight check for Local mode
    await page.route('**/api/preflight?profile=Balanced%20Mix*', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'X-Correlation-Id': 'test-correlation-456' },
        body: JSON.stringify({
          ok: true,
          stages: [
            {
              stage: 'Script',
              status: 'pass',
              provider: 'Ollama',
              message: 'Local LLM available',
            },
            {
              stage: 'TTS',
              status: 'pass',
              provider: 'Piper',
              message: 'Local TTS available',
            },
            {
              stage: 'Visuals',
              status: 'pass',
              provider: 'Stable Diffusion',
              message: 'SD WebUI available',
            },
          ],
        }),
      });
    });

    // Mock engine instances API
    await page.route('**/api/engines/instances', (route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          instances: [
            {
              engineId: 'stable-diffusion-webui',
              engineName: 'Stable Diffusion WebUI',
              installPath: 'C:\\Tools\\stable-diffusion-webui',
              port: 7860,
            },
          ],
        }),
      });
    });

    // Navigate to first-run wizard
    await page.goto('/setup');

    // Select Local mode
    const localCard = page.locator('text=Local Mode').locator('..');
    await localCard.click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 1: Hardware Detection
    await page.getByRole('button', { name: 'Next' }).click();
    await expect(page.getByText(/NVIDIA RTX 3080/)).toBeVisible({ timeout: 5000 });
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 2: Install Required Components
    await expect(page.getByText('Stable Diffusion WebUI')).toBeVisible();

    // Click "Use Existing" for Stable Diffusion
    const useExistingButtons = page.getByRole('button', { name: /use existing/i });
    const sdUseExistingButton = useExistingButtons.nth(1); // Assuming SD is second in list
    await sdUseExistingButton.click();

    // Fill in existing SD path
    await page.getByLabel(/install path/i).fill('C:\\Tools\\stable-diffusion-webui');
    await page
      .getByLabel(/executable path/i)
      .fill('C:\\Tools\\stable-diffusion-webui\\webui-user.bat');

    // Attach
    await page.getByRole('button', { name: /attach & validate/i }).click();
    await expect(page.getByText(/Use Existing.*Stable Diffusion/i)).not.toBeVisible({
      timeout: 5000,
    });

    // Continue to validation
    await page.getByRole('button', { name: 'Next' }).click();

    // Validate
    await page.getByRole('button', { name: 'Validate' }).click();
    await expect(page.getByText('All Set!')).toBeVisible({ timeout: 10000 });

    // Verify file locations show SD with Open Web UI button
    await expect(page.getByText(/Where are my files/i)).toBeVisible();
    await expect(page.getByText('Stable Diffusion WebUI')).toBeVisible();
    await expect(page.getByRole('button', { name: /open web ui/i })).toBeVisible();
  });

  test('should allow skipping optional components', async ({ page }) => {
    // Mock APIs
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

    await page.route('**/api/preflight?profile=Free-Only*', (route) => {
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

    await page.goto('/setup');

    // Navigate to step 2
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Look for optional items with Skip button
    const skipButtons = page.getByRole('button', { name: /skip/i });
    const count = await skipButtons.count();

    // At least one optional component should have skip button
    expect(count).toBeGreaterThan(0);

    // Click skip on first optional item
    if (count > 0) {
      await skipButtons.first().click();
    }
  });

  test('should show error when attach fails', async ({ page }) => {
    // Mock attach failure
    await page.route('**/api/engines/attach', (route) => {
      route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Invalid installation path',
        }),
      });
    });

    await page.goto('/setup');

    // Navigate to step 2
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Next' }).click();
    await page.getByRole('button', { name: 'Next' }).click();

    // Try to attach with invalid path
    const useExistingButton = page.getByRole('button', { name: /use existing/i }).first();
    await useExistingButton.click();

    await page.getByLabel(/install path/i).fill('/invalid/path');
    await page.getByRole('button', { name: /attach & validate/i }).click();

    // Should show error message
    await expect(page.getByText(/invalid installation path/i)).toBeVisible({ timeout: 3000 });
  });
});
