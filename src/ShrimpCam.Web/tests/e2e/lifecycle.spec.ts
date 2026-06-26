import { expect, test } from "@playwright/test";
import { mockShrimpCamApi, navigateInApp, signIn } from "./fixtures";

test("shows cached shell metadata while offline and reconnect guidance when online again", async ({ context, page }) => {
  await mockShrimpCamApi(page);
  await signIn(page);
  await navigateInApp(page, "/gallery");

  await context.setOffline(true);

  await expect(page.getByText("Offline shell active. Cached content may be stale.")).toBeVisible();
  await expect(page.getByRole("status").filter({ hasText: "Offline Shell" })).toContainText("Cached view available");
  await expect(page.getByText("Dashboard Cache")).toBeVisible();
  await expect(page.getByText("Gallery Cache")).toBeVisible();
  await expect(page.getByText("2 cached")).toBeVisible();
  await expect(page.getByText(/Potentially stale latest capture:/)).toBeVisible();

  await context.setOffline(false);

  await expect(page.getByText("Connection restored. Refresh a screen to pull the newest camera data.")).toBeVisible();
});

test("represents install prompt, dismissed install, and installed app lifecycle states", async ({ page }) => {
  await mockShrimpCamApi(page);
  await page.goto("/sign-in");

  await page.evaluate(() => {
    const event = new Event("beforeinstallprompt") as Event & {
      prompt: () => Promise<void>;
      userChoice: Promise<{ outcome: "dismissed"; platform: string }>;
    };
    event.prompt = () => Promise.resolve();
    event.userChoice = Promise.resolve({ outcome: "dismissed", platform: "web" });
    window.dispatchEvent(event);
  });

  await expect(page.getByText("Install Shrimp Cam for home-screen launch and standalone display.")).toBeVisible();
  await page.getByRole("button", { name: "Install app" }).click();
  await expect(page.getByText(/Install app|Add to Home screen/i)).toBeVisible();

  await page.evaluate(() => window.dispatchEvent(new Event("appinstalled")));

  await expect(page.getByText("Add Shrimp Cam to this device")).toHaveCount(0);
  await expect(page.getByRole("button", { name: /Install app|How to install/ })).toHaveCount(0);
});

test("expires protected session on unauthorized API response and renders unknown route recovery", async ({ page }) => {
  await mockShrimpCamApi(page);
  await signIn(page);

  let forceUnauthorized = true;
  await page.route("/settings", async (route) => {
    if (route.request().resourceType() === "document") {
      await route.fallback();
      return;
    }

    if (forceUnauthorized) {
      await route.fulfill({ status: 401, contentType: "application/json", body: JSON.stringify({ status: "unauthorized" }) });
      return;
    }

    await route.fallback();
  });
  await navigateInApp(page, "/settings").catch(() => undefined);

  await expect(page.getByRole("heading", { name: "Sign in to Shrimp Cam" })).toBeVisible();
  await expect(page.getByText("Your session expired. Sign in again to continue.")).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Primary" })).toHaveCount(0);

  forceUnauthorized = false;
  await signIn(page);
  await page.goto("/unknown-route");

  await expect(page.getByRole("heading", { name: "Not Found" })).toBeVisible();
  await expect(page.getByText("Head back to the dashboard to keep exploring the shell.")).toBeVisible();
  await page.getByRole("link", { name: "Return to Dashboard" }).click();
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
});

test("restores a valid saved session without requiring sign-in", async ({ page }) => {
  await mockShrimpCamApi(page);
  await page.addInitScript(() => {
    window.localStorage.setItem(
      "shrimpcam.session",
      JSON.stringify({
        sessionId: "stored-session",
        userId: "stored-user",
        userName: "admin",
        token: "stored-token",
        expiresAtUtc: "2099-06-26T19:50:00Z"
      })
    );
  });

  const captureAuthorizations: string[] = [];
  await page.route(/\/captures(?:\?.*)?$/, async (route) => {
    if (route.request().resourceType() === "document") {
      await route.fallback();
      return;
    }

    captureAuthorizations.push(route.request().headers().authorization ?? "");
    await route.fallback();
  });

  await page.goto("/dashboard");

  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  await expect(page.getByLabel("Session and install status")).toContainText("Signed in as admin.");
  expect(captureAuthorizations).toContain("Bearer stored-token");
  await expect(page.getByRole("heading", { name: "Sign in to Shrimp Cam" })).toHaveCount(0);
});

test("clears expired and corrupt saved sessions before protected routes render", async ({ page }) => {
  await mockShrimpCamApi(page);
  await page.addInitScript(() => {
    window.localStorage.setItem(
      "shrimpcam.session",
      JSON.stringify({
        sessionId: "expired-session",
        userId: "expired-user",
        userName: "admin",
        token: "expired-token",
        expiresAtUtc: "2000-01-01T00:00:00Z"
      })
    );
  });

  await page.goto("/dashboard");

  await expect(page.getByRole("heading", { name: "Sign in to Shrimp Cam" })).toBeVisible();
  await expect(page.getByText("Sign in to continue.")).toBeVisible();
  await expect(page.evaluate(() => window.localStorage.getItem("shrimpcam.session"))).resolves.toBeNull();

  await page.evaluate(() => window.localStorage.setItem("shrimpcam.session", "{not-json"));
  await page.goto("/gallery");

  await expect(page.getByRole("heading", { name: "Sign in to Shrimp Cam" })).toBeVisible();
  await expect(page.evaluate(() => window.localStorage.getItem("shrimpcam.session"))).resolves.toBeNull();
});

test("signs out with the active bearer token and clears the saved session", async ({ page }) => {
  await mockShrimpCamApi(page);

  const logoutAuthorizations: string[] = [];
  await page.route("/auth/logout", async (route) => {
    logoutAuthorizations.push(route.request().headers().authorization ?? "");
    await route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });

  await signIn(page);
  await page.getByRole("button", { name: "Sign out" }).click();

  await expect(page.getByRole("heading", { name: "Sign in to Shrimp Cam" })).toBeVisible();
  await expect(page.getByText("You have signed out.")).toBeVisible();
  expect(logoutAuthorizations).toEqual(["Bearer e2e-token"]);
  await expect(page.evaluate(() => window.localStorage.getItem("shrimpcam.session"))).resolves.toBeNull();
  await expect(page.getByRole("navigation", { name: "Primary" })).toHaveCount(0);
});

test("shows actionable dashboard failures without stale success messaging", async ({ page }) => {
  await mockShrimpCamApi(page, { healthStatus: 503, captureListStatus: 503 });
  await signIn(page);

  await expect(page.getByText("Dashboard data loaded")).toBeVisible();
  await expect(page.getByText("Health data is unavailable. Check diagnostics if this continues.")).toBeVisible();
  await expect(page.getByText("Capture history is unavailable. Try again after the service reconnects.")).toBeVisible();
  await expect(page.getByText("All dashboard sections responded. Refresh any time after a capture or reconnect.")).toHaveCount(0);
  await expect(page.getByText("Use refresh to retry, or open Settings to review system status.")).toBeVisible();
});
