import { expect, type Page } from "@playwright/test";

const pixel = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=",
  "base64"
);

export const healthResponse = {
  status: "Healthy",
  checkedAtUtc: "2026-06-25T19:50:00Z",
  components: {
    app: { status: "Healthy", detail: null },
    camera: { status: "Healthy", detail: "Logi C270 HD WebCam is online." },
    storage: { status: "Healthy", detail: "2 captures currently indexed." },
    database: { status: "Healthy", detail: null }
  },
  applicationVersion: "0.0.1.0",
  informationalVersion: "0.0.1-alpha+e2e",
  sourceRevision: "e2e",
  buildConfiguration: "Release"
};

export const captures = [
  {
    id: "scheduled-1950",
    relativeImagePath: "2026/06/25/20260625T195000000Z_scheduled.jpg",
    relativeMetadataPath: "2026/06/25/20260625T195000000Z_scheduled.json",
    fileName: "20260625T195000000Z_scheduled.jpg",
    sourceType: "Scheduled",
    capturedAtUtc: "2026-06-25T19:50:00Z",
    imageUrl: "/captures/scheduled-1950/image",
    metadataUrl: "/captures/scheduled-1950/metadata"
  },
  {
    id: "manual-1950",
    relativeImagePath: "2026/06/25/20260625T195005491Z_manual.jpg",
    relativeMetadataPath: "2026/06/25/20260625T195005491Z_manual.json",
    fileName: "20260625T195005491Z_manual.jpg",
    sourceType: "Manual",
    capturedAtUtc: "2026-06-25T19:50:05.491Z",
    imageUrl: "/captures/manual-1950/image",
    metadataUrl: "/captures/manual-1950/metadata"
  }
];

export const settingsResponse = {
  camera: {
    platform: "Windows",
    source: "Logi C270 HD WebCam",
    captureWidth: 1280,
    captureHeight: 720,
    streamWidth: 1280,
    streamHeight: 720,
    streamFramesPerSecond: 15,
    reconnectRetryAttempts: 2,
    reconnectBackoffSeconds: 1
  },
  capture: {
    enabled: true,
    intervalMinutes: 5,
    activeStartHourUtc: 6,
    activeEndHourUtc: 22,
    motionHighlightsEnabled: false,
    motionThreshold: 0.35,
    motionCooldownSeconds: 300
  },
  storage: {
    retentionDays: 30
  },
  security: {
    hostMode: "InternetExposed"
  }
};

type MockShrimpCamApiOptions = {
  captures?: typeof captures;
  emptyFilteredCaptures?: boolean;
  captureListStatus?: number;
  captureImageStatus?: number;
  healthStatus?: number;
  manualCaptureStatus?: number;
  manualCaptureBody?: unknown;
  settingsStatus?: number;
  settingsSaveStatus?: number;
  settingsSaveBody?: unknown;
  cameraDiscoveryStatus?: number;
  streamFailuresBeforeSuccess?: number;
};

function captureListBody(items: typeof captures, totalItems = items.length) {
  return {
    items,
    paging: {
      pageNumber: 1,
      pageSize: 25,
      totalItems,
      totalPages: totalItems > 0 ? 1 : 0,
      hasPreviousPage: false,
      hasNextPage: false
    }
  };
}

export async function mockShrimpCamApi(page: Page, options: MockShrimpCamApiOptions = {}) {
  let remainingStreamFailures = options.streamFailuresBeforeSuccess ?? 0;
  await page.route("/auth/login", async (route) => {
    const body = route.request().postDataJSON() as { userName?: string; password?: string };
    if (body.userName === "admin" && body.password === "AdminPass1234") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          sessionId: "e2e-session",
          userId: "e2e-user",
          token: "e2e-token",
          expiresAtUtc: "2026-06-26T19:50:00Z",
          userName: "admin",
          role: "Admin"
        })
      });
      return;
    }

    await route.fulfill({
      status: 401,
      contentType: "application/json",
      body: JSON.stringify({ status: "failed" })
    });
  });

  await page.route("/auth/logout", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });

  await page.route("/health", async (route) => {
    if (options.healthStatus && options.healthStatus >= 400) {
      await route.fulfill({ status: options.healthStatus, contentType: "application/json", body: JSON.stringify({ status: "failed" }) });
      return;
    }

    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(healthResponse) });
  });

  await page.route(/\/captures(?:\?.*)?$/, async (route) => {
    if (options.captureListStatus && options.captureListStatus >= 400) {
      await route.fulfill({ status: options.captureListStatus, contentType: "application/json", body: JSON.stringify({ status: "failed" }) });
      return;
    }

    const url = new URL(route.request().url());
    const fromUtc = url.searchParams.get("fromUtc");
    const sourceCaptures = options.captures ?? captures;
    const filteredCaptures = fromUtc
      ? options.emptyFilteredCaptures
        ? []
        : sourceCaptures.filter((capture) => capture.capturedAtUtc.startsWith("2026-06-25"))
      : sourceCaptures;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(captureListBody(filteredCaptures))
    });
  });

  await page.route("/captures/manual", async (route) => {
    if (options.manualCaptureStatus && options.manualCaptureStatus >= 400) {
      await route.fulfill({
        status: options.manualCaptureStatus,
        contentType: "application/json",
        body: JSON.stringify(options.manualCaptureBody ?? { status: "failed", reason: "Camera busy." })
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        status: "captured",
        sourceType: "Manual",
        capturedAtUtc: "2026-06-25T20:00:00Z",
        fileName: "20260625T200000000Z_manual.jpg",
        imagePath: "data/images/2026/06/25/20260625T200000000Z_manual.jpg",
        relativeImagePath: "2026/06/25/20260625T200000000Z_manual.jpg",
        metadataPath: "data/images/2026/06/25/20260625T200000000Z_manual.json"
      })
    });
  });

  await page.route(/\/captures\/[^/]+\/image$/, async (route) => {
    if (!route.request().headers().authorization) {
      await route.fulfill({ status: 401, contentType: "application/json", body: JSON.stringify({ status: "unauthorized" }) });
      return;
    }

    if (options.captureImageStatus && options.captureImageStatus >= 400) {
      await route.fulfill({ status: options.captureImageStatus, contentType: "application/json", body: JSON.stringify({ status: "failed" }) });
      return;
    }

    await route.fulfill({ status: 200, contentType: "image/png", body: pixel });
  });

  await page.route(/\/captures\/[^/]+\/metadata$/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ sourceType: "Scheduled" })
    });
  });

  await page.route(/\/stream\/live(?:\?.*)?$/, async (route) => {
    if (remainingStreamFailures > 0) {
      remainingStreamFailures -= 1;
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ status: "failed" }) });
      return;
    }

    await route.fulfill({ status: 200, contentType: "image/png", body: pixel });
  });

  await page.route("/settings", async (route) => {
    if (route.request().resourceType() === "document") {
      await route.fallback();
      return;
    }

    if (route.request().method() === "PUT") {
      if (options.settingsSaveStatus && options.settingsSaveStatus >= 400) {
        await route.fulfill({
          status: options.settingsSaveStatus,
          contentType: "application/json",
          body: JSON.stringify(options.settingsSaveBody ?? { status: "failed" })
        });
        return;
      }

      const saved = route.request().postDataJSON();
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(saved) });
      return;
    }

    if (options.settingsStatus && options.settingsStatus >= 400) {
      await route.fulfill({ status: options.settingsStatus, contentType: "application/json", body: JSON.stringify({ status: "failed" }) });
      return;
    }

    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(settingsResponse) });
  });

  await page.route(/\/cameras\?platform=.*/, async (route) => {
    if (options.cameraDiscoveryStatus && options.cameraDiscoveryStatus >= 400) {
      await route.fulfill({ status: options.cameraDiscoveryStatus, contentType: "application/json", body: JSON.stringify({ status: "failed" }) });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        platform: "Windows",
        cameras: [
          {
            displayName: "Integrated Webcam",
            devicePath: "@device_pnp_integrated",
            platform: "Windows"
          },
          {
            displayName: "Logi C270 HD WebCam",
            devicePath: "Logi C270 HD WebCam",
            platform: "Windows"
          }
        ]
      })
    });
  });
}

export async function signIn(page: Page) {
  await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Sign in to Shrimp Cam" })).toBeVisible();
  await page.getByLabel("Username").fill("admin");
  await page.getByLabel("Password").fill("AdminPass1234");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
}

export async function navigateInApp(page: Page, path: "/dashboard" | "/live" | "/gallery" | "/settings") {
  const labels: Record<typeof path, string> = {
    "/dashboard": "Dashboard",
    "/live": "Live",
    "/gallery": "Gallery",
    "/settings": "Settings"
  };
  await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: labels[path], exact: true }).evaluate((element) => (element as HTMLAnchorElement).click());
  await page.waitForURL(`**${path}`);
}
