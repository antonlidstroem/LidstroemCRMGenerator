// FIX (setup): Added versioned cache name and cleanup on activate.
// Previously 'lidstroem-v1' was never busted — users would receive stale assets
// after every deploy. Bump CACHE_NAME on each release to force a cache refresh.
const CACHE_NAME = 'lidstroem-v2';

self.addEventListener('install', () => self.skipWaiting());

// FIX: Activate now deletes all caches from previous versions before claiming clients.
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys
          .filter(key => key !== CACHE_NAME)
          .map(key => caches.delete(key))
      )
    ).then(() => clients.claim())
  );
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;
  event.respondWith(
    caches.open(CACHE_NAME).then(cache =>
      cache.match(event.request).then(cached => {
        const fetched = fetch(event.request).then(response => {
          if (response.ok) cache.put(event.request, response.clone());
          return response;
        });
        return cached || fetched;
      })
    )
  );
});
