import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { mockShrimpCamApi, navigateInApp, signIn } from "./fixtures";

async function expectNoSeriousAccessibilityViolations(page: Page) {
  const results = await new AxeBuilder({ page }).analyze();
  const seriousViolations = results.violations.filter((violation) => violation.impact === "serious" || violation.impact === "critical");

  expect(
    seriousViolations.map((violation) => ({
      id: violation.id,
      impact: violation.impact,
      description: violation.description,
      nodes: violation.nodes.map((node) => node.target.join(" "))
    }))
  ).toEqual([]);
}

test.beforeEach(async ({ page }) => {
  await mockShrimpCamApi(page);
});

test("sign-in and protected app routes have no serious accessibility violations", async ({ page }) => {
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { name: "Sign in to Shrimp Cam" })).toBeVisible();
  await expectNoSeriousAccessibilityViolations(page);

  await signIn(page);
  await expectNoSeriousAccessibilityViolations(page);

  await navigateInApp(page, "/live");
  await page.getByAltText("Live shrimp tank camera feed").dispatchEvent("load");
  await expect(page.getByText("Live stream is online.")).toBeVisible();
  await expectNoSeriousAccessibilityViolations(page);

  await navigateInApp(page, "/gallery");
  await expect(page.getByRole("heading", { name: "Gallery" })).toBeVisible();
  await expectNoSeriousAccessibilityViolations(page);

  await navigateInApp(page, "/settings");
  await expect(page.getByRole("heading", { name: "Settings" })).toBeVisible();
  await expectNoSeriousAccessibilityViolations(page);
});

test("offline and not-found recovery states have no serious accessibility violations", async ({ context, page }) => {
  await signIn(page);
  await navigateInApp(page, "/gallery");

  await context.setOffline(true);
  await expect(page.getByText("Offline shell active. Cached content may be stale.")).toBeVisible();
  await expect(page.getByRole("status").filter({ hasText: "Offline Shell" })).toContainText("Cached view available");
  await expectNoSeriousAccessibilityViolations(page);

  await context.setOffline(false);
  await expect(page.getByText("Connection restored. Refresh a screen to pull the newest camera data.")).toBeVisible();

  await page.goto("/unknown-route");
  await expect(page.getByRole("heading", { name: "Not Found" })).toBeVisible();
  await expectNoSeriousAccessibilityViolations(page);
});
