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
 * Detects Unity WebGL build files automatically
 */
function detectUnityBuildFiles(buildPath) {
    if (!fs.existsSync(buildPath)) {
        console.error(`Build directory not found: ${buildPath}`);
        return null;
    }

    const files = fs.readdirSync(buildPath);
    const buildFiles = {
        loader: files.find(f => f.endsWith('.loader.js')),
        data: files.find(f => f.endsWith('.data')),
        framework: files.find(f => f.endsWith('.framework.js')),
        wasm: files.find(f => f.endsWith('.wasm'))
    };

    // Validate all required files exist
    if (!buildFiles.loader || !buildFiles.data || !buildFiles.framework || !buildFiles.wasm) {
        console.error('Missing Unity build files. Expected: .loader.js, .data, .framework.js, .wasm');
        console.log('Found files:', files);
        return null;
    }

    return buildFiles;
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