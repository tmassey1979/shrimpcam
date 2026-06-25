import { expect, test } from "@playwright/test";
import { mockShrimpCamApi, signIn } from "./fixtures";

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
  await expect(page.getByText("20260625T195000000Z_scheduled.jpg from Scheduled")).toBeVisible();
  const latestSnapshot = page.getByRole("img", { name: /Latest shrimp tank snapshot/ });
  await expect(latestSnapshot).toBeVisible();
  await expect(latestSnapshot).toHaveAttribute("src", /^blob:/);
  expect(imageAuthorizations).toContain("Bearer e2e-token");
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
  await page.getByRole("link", { name: "Live" }).click({ force: true });

  await expect(page.getByRole("heading", { name: "Live" })).toBeVisible();
  await page.getByAltText("Live shrimp tank camera feed").dispatchEvent("load");
  await expect(page.getByText("Live stream is online.")).toBeVisible();

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
  await page.getByRole("link", { name: "Gallery" }).click({ force: true });

  await expect(page.getByRole("heading", { name: "Gallery" })).toBeVisible();
  await expect(page.getByText("2 captures found.")).toBeVisible();
  await expect(page.getByRole("button", { name: /20260625T195000000Z_scheduled.jpg/ })).toBeVisible();
  const captureImage = page.getByRole("img", { name: /Shrimp tank capture/ });
  await expect(captureImage).toBeVisible();
  await expect(captureImage).toHaveAttribute("src", /^blob:/);
  expect(imageAuthorizations).toContain("Bearer e2e-token");

  await page.getByLabel("Filter captures by day").fill("2026-06-25");
  await expect(page.getByText(/2 captures found for/)).toBeVisible();
  await page.getByRole("button", { name: "Clear filter" }).click();
  await expect(page.getByText("2 captures found.")).toBeVisible();
});

test("updates settings with discovered camera options", async ({ page }) => {
  await signIn(page);
  await page.getByRole("link", { name: "Settings" }).click({ force: true });

  await expect(page.getByRole("heading", { name: "Settings" })).toBeVisible();
  await expect(page.getByText("Found 2 cameras for Windows.")).toBeVisible();
  await page.getByLabel("Selected camera source").fill("Integrated Webcam");
  await page.getByLabel("Interval minutes").fill("10");
  await page.getByRole("button", { name: "Save settings" }).click();

  await expect(page.getByText("Settings saved. Refreshed values match the service response.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save settings" })).toBeDisabled();
});
