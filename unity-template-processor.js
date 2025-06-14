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
    width: 960,                               // Canvas width (default to working size)
    height: 600                               // Canvas height (default to working size)
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
        template = template.replace(/{{{ WIDTH }}}/g, CONFIG.width);
        template = template.replace(/{{{ HEIGHT }}}/g, CONFIG.height);

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

        // Cleanup PWA files that Unity generates but we don't need
        cleanupUnityPWAFiles();

        console.log('🎉 Template processing complete!');
        return true;
    } catch (error) {
        console.error('❌ Error processing template:', error.message);
        return false;
    }
}

/**
 * Auto-detects Unity WebGL build files by scanning the Build directory
 */
function detectUnityBuildFiles(buildDir) {
    if (!fs.existsSync(buildDir)) {
        console.error(`Build directory not found: ${buildDir}`);
        return null;
    }

    const files = fs.readdirSync(buildDir);
    const buildFiles = {
        loader: null,
        data: null,
        framework: null,
        wasm: null
    };

    // Find files by extension - matches your working pattern
    for (const file of files) {
        if (file.endsWith('.loader.js')) {
            buildFiles.loader = file;
        } else if (file.endsWith('.data')) {
            buildFiles.data = file;
        } else if (file.endsWith('.framework.js')) {
            buildFiles.framework = file;
        } else if (file.endsWith('.wasm')) {
            buildFiles.wasm = file;
        }
    }

    // Verify all required files found
    const missingFiles = Object.entries(buildFiles)
        .filter(([key, value]) => !value)
        .map(([key]) => key);

    if (missingFiles.length > 0) {
        console.error(`❌ Missing Unity build files: ${missingFiles.join(', ')}`);
        console.log('📁 Available files in Build directory:');
        files.forEach(file => console.log(`   ${file}`));
        return null;
    }

    return buildFiles;
}

/**
 * Removes Unity PWA files that conflict with Firebase hosting
 */
function cleanupUnityPWAFiles() {
    const filesToDelete = [
        path.join('.', 'manifest.webmanifest'),
        path.join('.', 'ServiceWorker.js')
    ];

    filesToDelete.forEach(file => {
        if (fs.existsSync(file)) {
            fs.unlinkSync(file);
            console.log(`🗑️  Removed Unity-generated file: ${path.basename(file)}`);
        }
    });

    // Create custom PWA manifest for Unreality3D
    createCustomPWAManifest();
    createCustomServiceWorker();
}

/**
 * Creates a custom PWA manifest that works with Firebase hosting
 */
function createCustomPWAManifest() {
    const manifest = {
        name: CONFIG.productName,
        short_name: CONFIG.productName.substring(0, 12),
        start_url: "./",
        display: "fullscreen",
        background_color: "#232323",
        theme_color: "#667eea"
        // Note: No icons reference since TemplateData folder doesn't exist in our workflow
    };

    fs.writeFileSync(path.join('.', 'manifest.webmanifest'), JSON.stringify(manifest, null, 2));
    console.log('✅ Created custom PWA manifest');
}

/**
 * Creates a minimal service worker for offline capability
 */
function createCustomServiceWorker() {
    const buildFiles = detectUnityBuildFiles(CONFIG.buildOutputPath);
    if (!buildFiles) {
        console.warn('⚠️  Could not detect build files for service worker cache');
        return;
    }

    const serviceWorker = `
// Unreality3D Service Worker for ${CONFIG.contentId}
const CACHE_NAME = '${CONFIG.contentId}-v${Date.now()}';
const urlsToCache = [
    './',
    './Build/${buildFiles.data}',
    './Build/${buildFiles.framework}',
    './Build/${buildFiles.loader}',
    './Build/${buildFiles.wasm}'
];

self.addEventListener('install', function(event) {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(function(cache) {
                console.log('Caching Unity WebGL files');
                return cache.addAll(urlsToCache);
            })
    );
});

self.addEventListener('fetch', function(event) {
    event.respondWith(
        caches.match(event.request)
            .then(function(response) {
                return response || fetch(event.request);
            }
        )
    );
});
`;

    fs.writeFileSync(path.join('.', 'ServiceWorker.js'), serviceWorker.trim());
    console.log('✅ Created custom service worker');
}

/**
 * Validates Firebase integration in template
 */
function validateFirebaseIntegration(template) {
    const requiredElements = [
        'firebase.initializeApp',
        'UnityCallTestFunction',
        'UnityCheckContentAccess',
        'UnityRequestPayment',
        'paypal.Buttons',
        'script.onload = () => {',  // Critical: Unity loader pattern
        'createUnityInstance(canvas, config'
    ];

    const missing = requiredElements.filter(element => !template.includes(element));
    
    if (missing.length > 0) {
        console.warn('⚠️  Missing Firebase/PayPal integrations:', missing.join(', '));
        return false;
    }
    
    console.log('✅ Firebase/PayPal integration validated');
    console.log('✅ Unity loader pattern validated');
    return true;
}

/**
 * Updates configuration from command line arguments
 */
function updateConfigFromArgs() {
    const args = process.argv.slice(2);
    
    for (let i = 0; i < args.length; i += 2) {
        const key = args[i].replace('--', '');
        const value = args[i + 1];
        
        if (CONFIG.hasOwnProperty(key) && value) {
            // Convert numeric values
            if (key === 'width' || key === 'height') {
                CONFIG[key] = parseInt(value, 10);
            } else {
                CONFIG[key] = value;
            }
            console.log(`📝 Updated ${key}: ${CONFIG[key]}`);
        }
    }
}

/**
 * Loads configuration from external JSON file if exists
 */
function loadConfigFile() {
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
        productVersion: '1.0.0',
        width: 960,
        height: 600
    };

    fs.writeFileSync(path.join('.', 'unity-template-config.json'), JSON.stringify(sampleConfig, null, 2));
    console.log('✅ Sample configuration created: unity-template-config.json');
}

/**
 * Displays deployment instructions for Firebase
 */
function showDeploymentInstructions() {
    console.log(`
🚀 DEPLOYMENT INSTRUCTIONS
=========================

Your Unity WebGL template has been processed successfully!

Next steps:
1. Copy Build/ folder and index.html to your Firebase project
2. Deploy to Firebase hosting:

   cd "D:\\Unreality3D"
   xcopy /E /I "Build" "public\\webgl-builds\\${CONFIG.contentId}\\Build"
   copy "index.html" "public\\webgl-builds\\${CONFIG.contentId}\\index.html"
   firebase deploy --only hosting

3. Test your build at:
   https://unreality3d2025.web.app/webgl-builds/${CONFIG.contentId}/

4. Verify Firebase Functions and PayPal integration work correctly
`);
}

// Command line interface
function showHelp() {
    console.log(`
Unity WebGL Template Processor for Unreality3D
==============================================

Fixes the Unity loading pattern and preserves Firebase/PayPal integration.

Usage:
  node unity-template-processor.js [options]

Options:
  --contentId <id>        Firestore content document ID
  --companyName <name>    Creator/company name
  --productName <name>    Unity product name
  --productVersion <ver>  Version number
  --width <pixels>        Canvas width (default: 960)
  --height <pixels>       Canvas height (default: 600)
  --help                  Show this help
  --create-config         Create sample config file

Examples:
  node unity-template-processor.js --contentId test-area-1 --productName "Virtual Gallery"
  node unity-template-processor.js --contentId my-scene --width 1920 --height 1080
  node unity-template-processor.js --create-config

Features:
  ✅ Auto-detects Unity build files (.data, .wasm, .framework.js, .loader.js)
  ✅ Fixes Unity loading pattern (loads script first, then calls createUnityInstance)
  ✅ Preserves Firebase + PayPal integration exactly
  ✅ Adds cache-busting for WebGL files
  ✅ Removes conflicting Unity PWA files
  ✅ Validates Firebase integration
  ✅ Creates custom PWA manifest and service worker
  ✅ Windows-compatible file operations
`);
}

// Main execution
if (require.main === module) {
    const args = process.argv.slice(2);
    
    if (args.includes('--help')) {
        showHelp();
        process.exit(0);
    }
    
    if (args.includes('--create-config')) {
        createSampleConfig();
        process.exit(0);
    }
    
    console.log('🔧 Loading configuration...');
    loadConfigFile();
    updateConfigFromArgs();
    
    console.log('📋 Current configuration:');
    Object.entries(CONFIG).forEach(([key, value]) => {
        console.log(`   ${key}: ${value}`);
    });
    
    const success = processUnityTemplate();
    
    if (success) {
        showDeploymentInstructions();
    }
    
    process.exit(success ? 0 : 1);
}

module.exports = { processUnityTemplate, CONFIG };