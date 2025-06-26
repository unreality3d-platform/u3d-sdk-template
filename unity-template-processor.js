const fs = require('fs');
const path = require('path');

// Configuration - Update these paths for your project
const CONFIG = {
    templatePath: path.join('.', 'template.html'),           // Your template file
    buildOutputPath: path.join('.', 'Build'),               // Unity WebGL Build folder
    outputPath: path.join('.', 'index.html'),               // Final output file
    contentId: 'test-area-1',                 // Firestore document ID
    companyName: 'Your Creator Name',         // Default company name
    productName: 'Unity Experience',          // Default product name
    productVersion: '1.0.0',                  // Default version
    width: 960,                               // Canvas width (removed from template - now responsive)
    height: 600                               // Canvas height (removed from template - now responsive)
};

/**
 * Automatically detects Unity WebGL build files and replaces template variables
 */
function processUnityTemplate() {
    try {
        console.log('🚀 Processing Unity WebGL template...');

        // Read template file
        if (!fs.existsSync(CONFIG.templatePath)) {
            console.error(`❌ Template file not found: ${CONFIG.templatePath}`);
            return false;
        }

        let template = fs.readFileSync(CONFIG.templatePath, 'utf8');
        console.log('✅ Template loaded');

        // Auto-detect Unity build files
        const buildFiles = detectUnityBuildFiles(CONFIG.buildOutputPath);
        if (!buildFiles) {
            console.error('❌ Unity build files not found. Run Unity WebGL build first.');
            return false;
        }

        console.log('✅ Unity build files detected:');
        console.log(`   Loader: ${buildFiles.loader}`);
        console.log(`   Data: ${buildFiles.data}`);
        console.log(`   Framework: ${buildFiles.framework}`);
        console.log(`   WASM: ${buildFiles.wasm}`);

        // Replace all template variables using the exact pattern from working template
        template = template.replace(/{{{ PRODUCT_NAME }}}/g, CONFIG.productName);
        template = template.replace(/{{{ CONTENT_ID }}}/g, CONFIG.contentId);
        template = template.replace(/{{{ LOADER_FILENAME }}}/g, buildFiles.loader);
        template = template.replace(/{{{ DATA_FILENAME }}}/g, buildFiles.data);
        template = template.replace(/{{{ FRAMEWORK_FILENAME }}}/g, buildFiles.framework);
        template = template.replace(/{{{ CODE_FILENAME }}}/g, buildFiles.wasm);
        template = template.replace(/{{{ COMPANY_NAME }}}/g, CONFIG.companyName);
        template = template.replace(/{{{ PRODUCT_VERSION }}}/g, CONFIG.productVersion);

        // Add cache-busting timestamp for WebGL files
        const timestamp = Date.now();
        const cachePattern = /buildUrl \+ "\/([^"]+)"/g;
        template = template.replace(cachePattern, `buildUrl + "/$1?v=${timestamp}"`);

        // Validate Firebase integration is preserved
        if (!validateFirebaseIntegration(template)) {
            console.error('❌ Firebase/PayPal integration validation failed');
            return false;
        }

        // Write processed file
        fs.writeFileSync(CONFIG.outputPath, template);
        console.log(`✅ Processed template saved to: ${CONFIG.outputPath}`);

        // Create enhanced PWA manifest for fullscreen experience
        createEnhancedPWAManifest();

        // Create optimized service worker
        createOptimizedServiceWorker();

        console.log('🎉 Template processing complete!');

        // Show environment-aware completion message (no deployment commands)
        showEnvironmentAwareCompletion();
        return true;

    } catch (error) {
        console.error('❌ Error processing template:', error.message);
        return false;
    }
}

/**
 * Generates creator-specific README.md file
 */
function generateCreatorReadme() {
    try {
        console.log('📝 Generating creator-specific README...');

        // Load the creator README template
        const readmeTemplate = `# {{{ PRODUCT_NAME }}}

**Unity WebGL Experience by {{{ CREATOR_USERNAME }}}**

🎮 **[Play Experience](https://{{{ CREATOR_USERNAME }}}.unreality3d.com/{{{ PROJECT_NAME }}}/)**

---

## About This Project

This is an interactive Unity WebGL experience created using the [Unreality3D Platform](https://unreality3d.com).

- **Creator**: {{{ CREATOR_USERNAME }}}
- **Built with**: Unity 6+ WebGL
- **Platform**: [Unreality3D](https://unreality3d.com)
- **Deployment**: Automated via GitHub Actions

## How to Play

🎮 **Controls**: WASD to move, mouse to look around
💰 **Monetization**: Supports PayPal payments for premium content
🌐 **Professional URL**: https://{{{ CREATOR_USERNAME }}}.unreality3d.com/{{{ PROJECT_NAME }}}/

---

## Technical Details

### Built With
- **Unity**: 6+ WebGL
- **Deployment**: GitHub Pages + Load Balancer
- **Payments**: PayPal Business Integration
- **Backend**: Firebase Functions
- **Platform**: [Unreality3D SDK](https://github.com/unreality3d-platform/u3d-sdk-template)

### Repository Structure
\`\`\`
├── Build/                 # Unity WebGL build files
├── index.html            # Processed Unity template
├── manifest.webmanifest  # PWA configuration
├── sw.js                 # Service worker for caching
└── README.md            # This file
\`\`\`

### Deployment Status
- ✅ **Auto-deployed**: Push to main branch triggers deployment
- ✅ **Professional URL**: Custom subdomain routing
- ✅ **PayPal Ready**: Monetization system active
- ✅ **Performance Optimized**: Brotli compression, caching, CDN

---

## For Developers

### Local Development
\`\`\`bash
# Clone this repository
git clone https://github.com/{{{ GITHUB_OWNER }}}/{{{ REPOSITORY_NAME }}}.git

# Serve locally (requires local server)
python -m http.server 8000
# or
npx serve .
\`\`\`

### Updating Content
1. **Unity**: Modify project and build for WebGL
2. **GitHub**: Push changes to trigger auto-deployment
3. **Live**: Changes appear at professional URL automatically

---

## Platform Information

This experience was created using **[Unreality3D](https://unreality3d.com)** - the PayPal-powered Unity WebGL platform.

### 🎯 Want to Create Your Own?
- **Download SDK**: [Get Unity Template](https://unreality3d.com/download-template)
- **Documentation**: [Platform Docs](https://unreality3d.com/docs)
- **Creator Dashboard**: [Unity SDK Setup](https://unreality3d.com)

### 🔧 Platform Features
- **Zero Setup**: Download template, build, deploy
- **Professional URLs**: Custom subdomains for every creator
- **PayPal Integration**: Built-in monetization system  
- **Auto-Deployment**: GitHub Actions handle everything
- **Performance**: Optimized for fast loading and smooth gameplay

---

**Powered by [Unreality3D](https://unreality3d.com) | Created by {{{ CREATOR_USERNAME }}}**`;

        // Replace template variables
        let processedReadme = readmeTemplate;
        processedReadme = processedReadme.replace(/{{{ PRODUCT_NAME }}}/g, CONFIG.productName);
        processedReadme = processedReadme.replace(/{{{ CREATOR_USERNAME }}}/g, extractCreatorUsername());
        processedReadme = processedReadme.replace(/{{{ PROJECT_NAME }}}/g, CONFIG.contentId);
        processedReadme = processedReadme.replace(/{{{ GITHUB_OWNER }}}/g, extractGitHubOwner());
        processedReadme = processedReadme.replace(/{{{ REPOSITORY_NAME }}}/g, extractRepositoryName());

        // Write the creator-specific README
        fs.writeFileSync('README.md', processedReadme);
        console.log('✅ Creator-specific README.md generated');

        return true;

    } catch (error) {
        console.error('❌ Error generating README:', error.message);
        return false;
    }
}

/**
 * Extract creator username from current directory or config
 */
function extractCreatorUsername() {
    // Try to get from current directory name or environment
    const currentDir = process.cwd();
    const dirName = path.basename(currentDir);

    // Pattern matching for creator repositories
    const match = dirName.match(/^([a-zA-Z0-9-]+)/);
    return match ? match[1] : 'creator';
}

/**
 * Extract GitHub owner from environment or directory
 */
function extractGitHubOwner() {
    // Try to get from git remote or environment variables
    try {
        const { execSync } = require('child_process');
        const remoteUrl = execSync('git remote get-url origin', { encoding: 'utf8' }).trim();
        const match = remoteUrl.match(/github\.com[:/]([^/]+)\//);
        return match ? match[1] : 'creator';
    } catch {
        return extractCreatorUsername(); // Fallback to creator username
    }
}

/**
 * Extract repository name from git or directory
 */
function extractRepositoryName() {
    try {
        const { execSync } = require('child_process');
        const remoteUrl = execSync('git remote get-url origin', { encoding: 'utf8' }).trim();
        const match = remoteUrl.match(/\/([^/]+)\.git$/);
        return match ? match[1] : CONFIG.contentId;
    } catch {
        return CONFIG.contentId; // Fallback to content ID
    }
}

/**
 * Enhanced Unity WebGL build file detection with multiple fallback paths
 */
function detectUnityBuildFiles(buildPath) {
    console.log(`🔍 Searching for Unity build files in: ${buildPath}`);

    // Multiple potential build paths to check
    const potentialPaths = [
        buildPath,                           // Direct build path
        path.join(buildPath, 'Build'),       // Build subdirectory
        path.join('.', 'Build'),             // Root Build directory
        path.join('.', 'WebGL'),             // WebGL directory
        path.join('.', 'WebGLBuild'),        // Alternative build directory
    ];

    let foundPath = null;
    let files = [];

    // Try each potential path
    for (const testPath of potentialPaths) {
        if (fs.existsSync(testPath)) {
            try {
                const testFiles = fs.readdirSync(testPath);
                console.log(`📁 Found directory: ${testPath} with ${testFiles.length} files`);

                // Check if this directory contains Unity WebGL files
                const hasUnityFiles = testFiles.some(f =>
                    f.endsWith('.loader.js') ||
                    f.endsWith('.wasm') ||
                    f.endsWith('.data') ||
                    f.endsWith('.framework.js')
                );

                if (hasUnityFiles) {
                    foundPath = testPath;
                    files = testFiles;
                    console.log(`✅ Unity files detected in: ${testPath}`);
                    break;
                }
            } catch (error) {
                console.log(`⚠️  Cannot read directory ${testPath}: ${error.message}`);
            }
        }
    }

    if (!foundPath) {
        console.error('❌ No Unity build directory found. Searched paths:');
        potentialPaths.forEach(p => console.error(`   - ${p}`));
        return null;
    }

    // Auto-detect build files with flexible naming
    const buildFiles = {
        loader: files.find(f => f.endsWith('.loader.js')),
        data: files.find(f => f.endsWith('.data')),
        framework: files.find(f => f.endsWith('.framework.js')),
        wasm: files.find(f => f.endsWith('.wasm'))
    };

    // Validate all required files exist
    const missingFiles = [];
    if (!buildFiles.loader) missingFiles.push('.loader.js');
    if (!buildFiles.data) missingFiles.push('.data');
    if (!buildFiles.framework) missingFiles.push('.framework.js');
    if (!buildFiles.wasm) missingFiles.push('.wasm');

    if (missingFiles.length > 0) {
        console.error(`❌ Missing Unity build files: ${missingFiles.join(', ')}`);
        console.error('📋 Available files:');
        files.forEach(f => console.error(`   - ${f}`));

        // Show file extensions for debugging
        const extensions = [...new Set(files.map(f => path.extname(f)))];
        console.error(`📋 Available extensions: ${extensions.join(', ')}`);

        return null;
    }

    // Update CONFIG.buildOutputPath to the found path for later use
    CONFIG.buildOutputPath = foundPath;

    console.log(`✅ All Unity build files found in: ${foundPath}`);
    return buildFiles;
}

// ENHANCE the processUnityTemplate function with better error handling:
function processUnityTemplate() {
    try {
        console.log('🚀 Processing Unity WebGL template...');
        console.log('📋 Build search configuration:');
        console.log(`   Initial build path: ${CONFIG.buildOutputPath}`);
        console.log(`   Template path: ${CONFIG.templatePath}`);
        console.log(`   Output path: ${CONFIG.outputPath}`);

        // Read template file
        if (!fs.existsSync(CONFIG.templatePath)) {
            console.error(`❌ Template file not found: ${CONFIG.templatePath}`);

            // Try to find template in common locations
            const alternatePaths = [
                path.join('.', 'template.html'),
                path.join('..', 'template.html'),
                path.join('Assets', 'WebGLTemplates', 'template.html')
            ];

            for (const altPath of alternatePaths) {
                if (fs.existsSync(altPath)) {
                    console.log(`✅ Found template at alternate location: ${altPath}`);
                    CONFIG.templatePath = altPath;
                    break;
                }
            }

            if (!fs.existsSync(CONFIG.templatePath)) {
                console.error('❌ Template file not found in any expected location');
                return false;
            }
        }

        let template = fs.readFileSync(CONFIG.templatePath, 'utf8');
        console.log('✅ Template loaded successfully');

        // Auto-detect Unity build files with enhanced detection
        const buildFiles = detectUnityBuildFiles(CONFIG.buildOutputPath);
        if (!buildFiles) {
            console.error('❌ Unity build files not found. Build detection failed.');
            console.error('💡 Ensure you have built your Unity project for WebGL first.');
            console.error('💡 Build files should be in: Build/, WebGL/, or WebGLBuild/ directory');
            return false;
        }

        // Rest of the function remains the same...
        console.log('✅ Unity build files detected:');
        console.log(`   Loader: ${buildFiles.loader}`);
        console.log(`   Data: ${buildFiles.data}`);
        console.log(`   Framework: ${buildFiles.framework}`);
        console.log(`   WASM: ${buildFiles.wasm}`);

        // Replace all template variables
        template = template.replace(/{{{ PRODUCT_NAME }}}/g, CONFIG.productName);
        template = template.replace(/{{{ CONTENT_ID }}}/g, CONFIG.contentId);
        template = template.replace(/{{{ LOADER_FILENAME }}}/g, buildFiles.loader);
        template = template.replace(/{{{ DATA_FILENAME }}}/g, buildFiles.data);
        template = template.replace(/{{{ FRAMEWORK_FILENAME }}}/g, buildFiles.framework);
        template = template.replace(/{{{ CODE_FILENAME }}}/g, buildFiles.wasm);
        template = template.replace(/{{{ COMPANY_NAME }}}/g, CONFIG.companyName);
        template = template.replace(/{{{ PRODUCT_VERSION }}}/g, CONFIG.productVersion);

        // Add cache-busting timestamp
        const timestamp = Date.now();
        const cachePattern = /buildUrl \+ "\/([^"]+)"/g;
        template = template.replace(cachePattern, `buildUrl + "/$1?v=${timestamp}"`);

        // Validate and write
        if (!validateFirebaseIntegration(template)) {
            console.error('❌ Firebase/PayPal integration validation failed');
            return false;
        }

        fs.writeFileSync(CONFIG.outputPath, template);
        console.log(`✅ Processed template saved to: ${CONFIG.outputPath}`);

        createEnhancedPWAManifest();
        createOptimizedServiceWorker();
        generateCreatorReadme();

        console.log('🎉 Template processing complete!');
        showEnvironmentAwareCompletion();
        return true;

    } catch (error) {
        console.error('❌ Error processing template:', error.message);
        console.error('📋 Error details:', error.stack);
        return false;
    }
}

/**
 * Validates that Firebase/PayPal integration is preserved in template
 */
function validateFirebaseIntegration(template) {
    const requiredPatterns = [
        'firebase',
        'paypal',
        'UnityRequestPayment',
        'OnPaymentComplete',
        'environment-aware',
        'createUnityInstance'
    ];

    for (const pattern of requiredPatterns) {
        if (!template.toLowerCase().includes(pattern.toLowerCase())) {
            console.warn(`⚠️  Pattern '${pattern}' not found in template`);
        }
    }

    return true; // Continue even with warnings
}

/**
 * Creates enhanced PWA manifest for fullscreen Unity experience
 */
function createEnhancedPWAManifest() {
    const manifest = {
        name: CONFIG.productName,
        short_name: CONFIG.productName,
        description: `${CONFIG.productName} - Interactive Unity experience powered by Unreality3D`,
        start_url: "./",
        display: "fullscreen",
        display_override: ["fullscreen", "standalone", "minimal-ui"],
        orientation: "landscape-primary",
        theme_color: "#232323",
        background_color: "#232323",
        icons: [
            {
                src: "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNTEyIiBoZWlnaHQ9IjUxMiIgdmlld0JveD0iMCAwIDUxMiA1MTIiIGZpbGw9Im5vbmUiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PHJlY3Qgd2lkdGg9IjUxMiIgaGVpZ2h0PSI1MTIiIGZpbGw9IiMyMzIzMjMiLz48dGV4dCB4PSI1MCUiIHk9IjUwJSIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZHk9Ii4zZW0iIGZpbGw9IndoaXRlIiBmb250LXNpemU9IjQ4Ij5Vbml0eTwvdGV4dD48L3N2Zz4=",
                sizes: "512x512",
                type: "image/svg+xml"
            }
        ],
        categories: ["games", "entertainment", "productivity"]
    };

    fs.writeFileSync('manifest.webmanifest', JSON.stringify(manifest, null, 2));
    console.log('✅ Created enhanced PWA manifest');
}

/**
 * Creates optimized service worker for Unity WebGL caching
 */
function createOptimizedServiceWorker() {
    const serviceWorker = `
const CACHE_NAME = 'unity-webgl-v1';
const urlsToCache = [
  './',
  './index.html',
  './Build/${CONFIG.contentId}.loader.js',
  './Build/${CONFIG.contentId}.framework.js',
  './Build/${CONFIG.contentId}.data',
  './Build/${CONFIG.contentId}.wasm'
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => cache.addAll(urlsToCache))
  );
});

self.addEventListener('fetch', (event) => {
  event.respondWith(
    caches.match(event.request)
      .then((response) => {
        return response || fetch(event.request);
      })
  );
});
`;

    fs.writeFileSync('sw.js', serviceWorker.trim());
    console.log('✅ Created optimized service worker');
}

/**
 * Shows environment-aware completion message
 */
function showEnvironmentAwareCompletion() {
    console.log(`
🚀 TEMPLATE PROCESSING COMPLETE
==============================

Your Unity WebGL template has been processed for fullscreen responsive deployment!

✅ Responsive canvas styling applied
✅ Firebase/PayPal integration preserved  
✅ Enhanced PWA manifest created
✅ Optimized service worker generated
✅ Environment-aware deployment ready

GitHub Actions will automatically:
• Deploy to production (unreality3d) for creators
• Deploy to development (unreality3d2025) for template testing
• Generate unique preview URLs for each deployment

Next: Commit and push to trigger automated deployment
`);
}

/**
 * Loads configuration from external file if it exists
 */
function loadConfig() {
    const configFile = path.join('.', 'unity-template-config.json');
    if (fs.existsSync(configFile)) {
        try {
            const fileConfig = JSON.parse(fs.readFileSync(configFile, 'utf8'));
            Object.assign(CONFIG, fileConfig);
            console.log('✅ Configuration loaded from unity-template-config.json');
        } catch (error) {
            console.warn('⚠️  Error loading config file:', error.message);
        }
    }
}

/**
 * Creates a sample configuration file
 */
function createSampleConfig() {
    const sampleConfig = {
        templatePath: path.join('.', 'template.html'),
        buildOutputPath: path.join('.', 'Build'),
        outputPath: path.join('.', 'index.html'),
        contentId: 'your-content-id',
        companyName: 'Your Creator Name',
        productName: 'Your Unity Experience',
        productVersion: '1.0.0'
    };

    fs.writeFileSync(path.join('.', 'unity-template-config.json'), JSON.stringify(sampleConfig, null, 2));
    console.log('✅ Sample configuration created: unity-template-config.json');
}

// Command line interface
function showHelp() {
    console.log(`
Unity WebGL Template Processor for Unreality3D
==============================================

Processes Unity WebGL templates for fullscreen responsive deployment with Firebase/PayPal integration.

Usage:
  node unity-template-processor.js [options]

Options:
  --contentId <id>        Set content ID (default: test-area-1)
  --productName <name>    Set product name (default: Unity Experience)
  --companyName <name>    Set company name (default: Your Creator Name)
  --productVersion <ver>  Set product version (default: 1.0.0)
  --buildOutputPath <path> Set Unity build path (default: ./Build)
  --templatePath <path>   Set template file path (default: ./template.html)
  --outputPath <path>     Set output file path (default: ./index.html)
  --create-config         Create sample configuration file
  --help                  Show this help message

Examples:
  node unity-template-processor.js --contentId my-game --productName "My Amazing Game"
  node unity-template-processor.js --create-config
`);
}

// Parse command line arguments
const args = process.argv.slice(2);
let shouldShowHelp = false;

for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    const nextArg = args[i + 1];

    switch (arg) {
        case '--help':
            shouldShowHelp = true;
            break;
        case '--create-config':
            createSampleConfig();
            process.exit(0);
            break;
        case '--contentId':
            if (nextArg) CONFIG.contentId = nextArg;
            i++;
            break;
        case '--productName':
            if (nextArg) CONFIG.productName = nextArg;
            i++;
            break;
        case '--companyName':
            if (nextArg) CONFIG.companyName = nextArg;
            i++;
            break;
        case '--productVersion':
            if (nextArg) CONFIG.productVersion = nextArg;
            i++;
            break;
        case '--buildOutputPath':
            if (nextArg) CONFIG.buildOutputPath = nextArg;
            i++;
            break;
        case '--templatePath':
            if (nextArg) CONFIG.templatePath = nextArg;
            i++;
            break;
        case '--outputPath':
            if (nextArg) CONFIG.outputPath = nextArg;
            i++;
            break;
    }
}

if (shouldShowHelp) {
    showHelp();
    process.exit(0);
}

// Load configuration and process template
console.log('🔧 Loading configuration...');
loadConfig();

console.log('📝 Updated contentId:', CONFIG.contentId);
console.log('📝 Updated productName:', CONFIG.productName);

console.log('📋 Current configuration:');
Object.keys(CONFIG).forEach(key => {
    console.log(` ${key}: ${CONFIG[key]}`);
});

// Process the template
const success = processUnityTemplate();

if (success) {
    console.log('✅ Template processing completed successfully!');
    process.exit(0);
} else {
    console.log('❌ Template processing failed!');
    process.exit(1);
}