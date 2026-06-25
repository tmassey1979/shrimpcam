import { expect, test } from "@playwright/test";
import { mockShrimpCamApi, navigateInApp, signIn } from "./fixtures";

test.beforeEach(async ({ page }) => {
  await mockShrimpCamApi(page);
});

test("protects routes and signs in to the dashboard", async ({ page }) => {
  const imageAuthorizations: Array<string | undefined> = [];
  page.on("request", (request) => {
    if (/\/captures\/[^/]+\/image$/.test(new URL(request.url()).pathname)) {
      imageAuthorizations.push(request.headers().authorization);
    }
  });

  await signIn(page);

  await expect(page.getByText("Signed in as admin.")).toBeVisible();
  await expect(page.getByText("Logi C270 HD WebCam is online.")).toBeVisible();
  await expect(page.getByText("Camera Status")).toBeVisible();
  await expect(page.getByText("Next Timelapse")).toBeVisible();
  await expect(page.getByLabel("Storage usage")).toContainText("Storage Usage");
  await expect(page.getByText("20260625T195000000Z_scheduled.jpg")).toBeVisible();
  const latestSnapshot = page.getByRole("img", { name: /Latest shrimp tank snapshot/ });
  await expect(latestSnapshot).toBeVisible();
  await expect(latestSnapshot).toHaveAttribute("src", /^blob:/);
  expect(imageAuthorizations).toContain("Bearer e2e-token");
  await expect(page.getByRole("link", { name: /Watch your tank/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Browse snapshots/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Capture Now/ })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Primary" })).toBeVisible();
});

test("renders the reference-led aquarium shell without the old heavy header", async ({ page }) => {
  await signIn(page);

  await expect(page.getByRole("banner")).toContainText("Shrimp Cam");
  await expect(page.getByRole("banner")).toContainText("Your tank. Always in view.");
  await expect(page.getByText("Reef watch")).toHaveCount(0);
  await expect(page.getByText("Secure Shell")).toHaveCount(0);
  await expect(page.getByLabel("Session and install status")).toContainText("Signed in as admin.");
  await expect(page.getByRole("navigation", { name: "Primary" })).toBeVisible();

  await page.setViewportSize({ width: 1024, height: 900 });
  await expect(page.getByRole("banner")).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Primary" })).toBeVisible();
});

test("shows install guidance when the PWA is not installed", async ({ page }) => {
  await page.goto("/sign-in");

  await expect(page.getByText("Add Shrimp Cam to this device")).toBeVisible();
  await expect(page.getByRole("button", { name: "How to install" })).toBeVisible();
  await page.getByRole("button", { name: "How to install" }).click();
  await expect(page.getByText(/Install app|Add to Home screen/i)).toBeVisible();
});

test("captures a manual snapshot from live view after stream recovery", async ({ page }) => {
  await signIn(page);
  await navigateInApp(page, "/live");

  await expect(page.getByRole("heading", { name: "Live" })).toBeVisible();
  await expect(page.getByLabel("Immersive live camera stage")).toBeVisible();
  await expect(page.getByLabel("Live camera controls")).toBeVisible();
  await page.getByAltText("Live shrimp tank camera feed").dispatchEvent("load");
  await expect(page.getByText("Live stream is online.")).toBeVisible();
  await expect(page.getByText("Capture the moment")).toBeVisible();

  await page.getByRole("button", { name: "Capture snapshot" }).click();
  await expect(page.getByText("Snapshot captured. Gallery history will include the new still image.")).toBeVisible();
  await expect(page.getByText("20260625T200000000Z_manual.jpg")).toBeVisible();
});

test("browses gallery captures and applies a day filter", async ({ page }) => {
  const imageAuthorizations: Array<string | undefined> = [];
  page.on("request", (request) => {
    if (/\/captures\/[^/]+\/image$/.test(new URL(request.url()).pathname)) {
      imageAuthorizations.push(request.headers().authorization);
    }
  });

  await signIn(page);
  await navigateInApp(page, "/gallery");

  await expect(page.getByRole("heading", { name: "Gallery" })).toBeVisible();
  await expect(page.getByText("2 captures found.")).toBeVisible();
  await expect(page.getByLabel("Capture timeline days")).toContainText("Jun 25, 2026");
  await expect(page.getByLabel("Capture source filters")).toContainText("Scheduled");
  await expect(page.getByLabel("Capture source filters")).toContainText("Manual");
  await expect(page.getByLabel("Capture thumbnail timeline")).toBeVisible();
  await expect(page.getByText("Featured Capture")).toBeVisible();
  await expect(page.getByRole("button", { name: /20260625T195000000Z_scheduled.jpg/ })).toBeVisible();
  const captureImage = page.getByRole("img", { name: /Shrimp tank capture/ });
  await expect(captureImage).toBeVisible();
  await expect(captureImage).toHaveAttribute("src", /^blob:/);
  expect(imageAuthorizations).toContain("Bearer e2e-token");

  await page.getByRole("button", { name: /20260625T195005491Z_manual.jpg/ }).click();
  await expect(page.getByLabel("Focused capture viewer")).toContainText("Manual");
  await expect(page.getByLabel("Gallery capture actions")).toBeVisible();

  await page.getByLabel("Filter captures by day").fill("2026-06-25");
  await expect(page.getByText(/2 captures found for/)).toBeVisible();
  await page.getByRole("button", { name: "Clear filter" }).click();
  await expect(page.getByText("2 captures found.")).toBeVisible();
});

test("updates settings with discovered camera options", async ({ page }) => {
  await signIn(page);
  await navigateInApp(page, "/settings");

  await expect(page.getByRole("heading", { name: "Settings" })).toBeVisible();
  await expect(page.getByText("Found 2 cameras for Windows.")).toBeVisible();
  await page.getByLabel("Selected camera source").fill("Integrated Webcam");
  await page.getByLabel("Interval minutes").fill("10");
  await page.getByRole("button", { name: "Save settings" }).click();

  await expect(page.getByText("Settings saved. Refreshed values match the service response.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save settings" })).toBeDisabled();
});
