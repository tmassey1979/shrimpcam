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
