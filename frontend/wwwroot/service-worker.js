// Minimal service worker — enables PWA install prompt.
// Extend with caching strategies as needed.
self.addEventListener('install',  () => self.skipWaiting());
self.addEventListener('activate', () => clients.claim());
