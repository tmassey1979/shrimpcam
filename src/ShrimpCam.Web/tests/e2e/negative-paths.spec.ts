import { expect, test } from "@playwright/test";
import { mockShrimpCamApi, navigateInApp, signIn } from "./fixtures";

test("shows actionable sign-in validation and failure feedback", async ({ page }) => {
  await mockShrimpCamApi(page);
  await page.goto("/sign-in");

  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("alert")).toHaveText("Enter your username and password to continue.");

  await page.getByLabel("Username").fill("admin");
  await page.getByLabel("Password").fill("wrong-password");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page.getByRole("alert")).toHaveText("We could not sign you in. Check your username and password, then try again.");
  await expect(page.getByLabel("Password")).toHaveValue("");
});

test("communicates live stream and manual snapshot failures", async ({ page }) => {
  await mockShrimpCamApi(page, {
    manualCaptureStatus: 503,
    manualCaptureBody: { status: "failed", reason: "Camera busy." },
    streamFailuresBeforeSuccess: 1
  });
  await signIn(page);
  await navigateInApp(page, "/live");

  await expect(page.getByText("Stream unavailable")).toBeVisible();
  await expect(page.getByLabel("Immersive live camera stage")).toBeVisible();
  await expect(page.getByLabel("Live camera controls")).toBeVisible();
  await expect(page.getByRole("button", { name: "Capture snapshot" })).toBeDisabled();

  await page.getByRole("button", { name: "Retry stream" }).click();
  await expect(page.getByText("Live stream is online.")).toBeVisible();

  await page.getByRole("button", { name: "Capture snapshot" }).click();
  await expect(page.getByRole("alert")).toHaveText("Manual snapshot failed. Camera busy.");
  await expect(page.getByRole("button", { name: "Capture snapshot" })).toBeEnabled();
});

test("keeps gallery usable for empty filters and protected image failures", async ({ page }) => {
  await mockShrimpCamApi(page, { emptyFilteredCaptures: true, captureImageStatus: 500 });
  await signIn(page);
  await navigateInApp(page, "/gallery");

  await expect(page.getByText("Image unavailable")).toBeVisible();
  await page.getByLabel("Filter captures by day").fill("2026-06-24");
  await expect(page.getByText("0 of 0 captures shown for Jun 24, 2026.")).toBeVisible();
  await expect(page.getByLabel("Capture timeline days")).toContainText("No timeline days loaded");
  await expect(page.getByText("No captures found")).toBeVisible();
  await page.getByRole("button", { name: "Clear date filter" }).click();
  await expect(page.getByText("2 of 2 captures shown.")).toBeVisible();
  await page.getByLabel("Search captures").fill("does-not-exist");
  await expect(page.getByText("No matching captures")).toBeVisible();
  await page.getByRole("button", { name: "Clear gallery filters" }).click();
  await expect(page.getByText("2 of 2 captures shown.")).toBeVisible();
});

test("recovers gallery browsing after capture history reload succeeds", async ({ page }) => {
  await mockShrimpCamApi(page);

  let captureHistoryRequests = 0;
  await page.route(/\/captures(?:\?.*)?$/, async (route) => {
    captureHistoryRequests += 1;
    if (captureHistoryRequests === 2) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ status: "failed" }) });
      return;
    }

    await route.fallback();
  });

  await signIn(page);
  await navigateInApp(page, "/gallery");

  await expect(page.getByRole("alert")).toHaveText("We could not load captures. Check the connection and try again.");
  await expect(page.getByText("Select a capture")).toBeVisible();
  await expect(page.getByLabel("Capture timeline days")).toContainText("No timeline days loaded");

  await page.getByLabel("Filter captures by day").fill("2026-06-25");

  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(page.getByText(/2 of 2 captures shown for/)).toBeVisible();
  await expect(page.getByLabel("Capture timeline days")).toContainText("Jun 25, 2026");
  await expect(page.getByRole("img", { name: /Shrimp tank capture/ })).toHaveAttribute("src", /^blob:/);
});

test("blocks invalid settings and preserves edits after server rejection", async ({ page }) => {
  await mockShrimpCamApi(page, { settingsSaveStatus: 403, cameraDiscoveryStatus: 503 });
  await signIn(page);
  await navigateInApp(page, "/settings");
  await expect(page.getByRole("heading", { name: "Settings" })).toBeVisible();
  await expect(page.getByText("Camera discovery failed. You can keep the saved source or enter a custom source.")).toBeVisible();

  await page.getByLabel("Selected camera source").fill("");
  await page.getByRole("button", { name: "Save settings" }).click();
  await expect(page.getByText("Fix the highlighted settings before saving.")).toBeVisible();
  await expect(page.getByText("Camera source is required.")).toBeVisible();

  await page.getByLabel("Selected camera source").fill("Integrated Webcam");
  await page.getByLabel("Interval minutes").fill("10");
  await page.getByRole("button", { name: "Save settings" }).click();
  await expect(page.getByText("Only administrators can update Shrimp Cam settings.")).toBeVisible();
  await expect(page.getByLabel("Selected camera source")).toHaveValue("Integrated Webcam");
});

test("recovers settings form after the settings API becomes available", async ({ page }) => {
  await mockShrimpCamApi(page);

  let failSettingsLoad = true;
  await page.route("/settings", async (route) => {
    if (route.request().resourceType() === "document") {
      await route.fallback();
      return;
    }

    if (failSettingsLoad && route.request().method() === "GET") {
      failSettingsLoad = false;
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ status: "failed" }) });
      return;
    }

    await route.fallback();
  });

  await signIn(page);
  await navigateInApp(page, "/settings");

  await expect(page.getByText("Settings are unavailable. Sign in as an administrator or retry after the service reconnects.")).toBeVisible();
  await expect(page.getByText("Settings unavailable")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save settings" })).toHaveCount(0);

  await page.getByRole("button", { name: "Refresh" }).click();

  await expect(page.getByText("Found 2 cameras for Windows.")).toBeVisible();
  await expect(page.getByLabel("Settings summary")).toContainText("Logi C270 HD WebCam");
  await expect(page.getByRole("button", { name: "Save settings" })).toBeDisabled();
});
