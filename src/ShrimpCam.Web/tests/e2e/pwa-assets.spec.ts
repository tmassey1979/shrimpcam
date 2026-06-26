import { expect, test } from "@playwright/test";

test("publishes an installable manifest from the production bundle", async ({ request }) => {
  const response = await request.get("/manifest.webmanifest");

  expect(response.ok()).toBeTruthy();
  expect(response.headers()["content-type"]).toContain("application/manifest+json");

  const manifest = (await response.json()) as {
    name?: string;
    short_name?: string;
    display?: string;
    start_url?: string;
    scope?: string;
    theme_color?: string;
    background_color?: string;
    icons?: Array<{ src?: string; sizes?: string; type?: string; purpose?: string }>;
  };

  expect(manifest.name).toBe("Shrimp Cam");
  expect(manifest.short_name).toBe("Shrimp Cam");
  expect(manifest.display).toBe("standalone");
  expect(manifest.start_url).toBe("/");
  expect(manifest.scope).toBe("/");
  expect(manifest.theme_color).toBe("#0f5f73");
  expect(manifest.background_color).toBe("#081a20");
  expect(manifest.icons).toEqual(
    expect.arrayContaining([
      expect.objectContaining({
        src: "/icons/icon.svg",
        sizes: "any",
        type: "image/svg+xml",
        purpose: "any"
      })
    ])
  );

  const iconResponse = await request.get("/icons/icon.svg");
  expect(iconResponse.ok()).toBeTruthy();
  expect(iconResponse.headers()["content-type"]).toContain("image/svg+xml");
});

test("serves service worker assets that precache the app shell", async ({ request }) => {
  const indexResponse = await request.get("/");
  expect(indexResponse.ok()).toBeTruthy();
  await expect(indexResponse.text()).resolves.toContain("manifest.webmanifest");

  const serviceWorkerResponse = await request.get("/sw.js");
  expect(serviceWorkerResponse.ok()).toBeTruthy();
  expect(serviceWorkerResponse.headers()["content-type"]).toContain("javascript");

  const serviceWorker = await serviceWorkerResponse.text();
  expect(serviceWorker).toContain("precacheAndRoute");
  expect(serviceWorker).toContain("index.html");
  expect(serviceWorker).toContain("manifest.webmanifest");
  expect(serviceWorker).toContain("icons/icon.svg");
});
