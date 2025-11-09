const { chromium } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const filePath = 'file://' + path.resolve('/tmp/ffmpeg-setup-visual-demo.html');
  
  await page.goto(filePath);
  await page.setViewportSize({ width: 1200, height: 2400 });
  
  await page.screenshot({ 
    path: '/tmp/ffmpeg-setup-screenshot.png',
    fullPage: true 
  });
  
  await browser.close();
  console.log('Screenshot saved to /tmp/ffmpeg-setup-screenshot.png');
})();
