const cacheName = "Your Creator Name-Unreality3D Test Scene-1.0.0";
const contentToCache = [
    "Build/8b11e7e756a9d3c2a2414019eb49116a.loader.js",
    "Build/179516d939a2b1d5c2c15c35a9d653be.framework.js",
    "Build/7dfbcc1b5929ab0dd395ac2697d71d82.data",
    "Build/63af4bdee5f169360128d2a4eebe2204.wasm",
    "TemplateData/style.css"

];

self.addEventListener('install', function (e) {
    console.log('[Service Worker] Install');
    
    e.waitUntil((async function () {
      const cache = await caches.open(cacheName);
      console.log('[Service Worker] Caching all: app shell and content');
      await cache.addAll(contentToCache);
    })());
});

self.addEventListener('fetch', function (e) {
    e.respondWith((async function () {
      let response = await caches.match(e.request);
      console.log(`[Service Worker] Fetching resource: ${e.request.url}`);
      if (response) { return response; }

      response = await fetch(e.request);
      const cache = await caches.open(cacheName);
      console.log(`[Service Worker] Caching new resource: ${e.request.url}`);
      cache.put(e.request, response.clone());
      return response;
    })());
});
