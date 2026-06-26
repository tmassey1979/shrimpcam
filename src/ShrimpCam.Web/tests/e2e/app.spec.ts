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

  const shellStyles = await page.evaluate(() => {
    const body = window.getComputedStyle(document.body);
    const header = window.getComputedStyle(document.querySelector(".app-header") as HTMLElement);
    const statusCard = window.getComputedStyle(document.querySelector(".shell-status-card") as HTMLElement);
    const nav = window.getComputedStyle(document.querySelector(".bottom-nav") as HTMLElement);
    const activeNav = window.getComputedStyle(document.querySelector(".nav-link.active") as HTMLElement);
    const dashboard = window.getComputedStyle(document.querySelector(".screen-dashboard") as HTMLElement);
    const snapshot = window.getComputedStyle(document.querySelector(".snapshot-preview") as HTMLElement);
    const primaryButton = window.getComputedStyle(document.querySelector(".primary-button") as HTMLElement);

    return {
      bodyBackground: body.backgroundImage,
      headerMinHeight: Number.parseFloat(header.minHeight),
      statusRadius: Number.parseFloat(statusCard.borderRadius),
      navPosition: nav.position,
      navRadius: Number.parseFloat(nav.borderRadius),
      activeNavBackground: activeNav.backgroundImage,
      dashboardBackground: dashboard.backgroundImage,
      snapshotMinHeight: Number.parseFloat(snapshot.minHeight),
      primaryButtonRadius: Number.parseFloat(primaryButton.borderRadius)
    };
  });
  expect(shellStyles.bodyBackground).toContain("gradient");
  expect(shellStyles.headerMinHeight).toBeGreaterThanOrEqual(130);
  expect(shellStyles.statusRadius).toBeGreaterThanOrEqual(20);
  expect(shellStyles.navPosition).toBe("fixed");
  expect(shellStyles.navRadius).toBeGreaterThanOrEqual(28);
  expect(shellStyles.activeNavBackground).toContain("gradient");
  expect(shellStyles.dashboardBackground).toContain("gradient");
  expect(shellStyles.snapshotMinHeight).toBeGreaterThanOrEqual(280);
  expect(shellStyles.primaryButtonRadius).toBeGreaterThanOrEqual(30);

  const navBox = await page.getByRole("navigation", { name: "Primary" }).boundingBox();
  expect(navBox?.y ?? 0).toBeGreaterThan(700);
  for (const link of await page.getByRole("navigation", { name: "Primary" }).getByRole("link").all()) {
    const box = await link.boundingBox();
    expect(box?.height ?? 0).toBeGreaterThanOrEqual(44);
    expect(box?.width ?? 0).toBeGreaterThanOrEqual(44);
  }

  await page.keyboard.press("Tab");
  const focusStyle = await page.evaluate(() => {
    const active = document.activeElement as HTMLElement;
    const style = window.getComputedStyle(active);
    return {
      outlineWidth: Number.parseFloat(style.outlineWidth),
      boxShadow: style.boxShadow
    };
  });
  expect(focusStyle.outlineWidth > 0 || focusStyle.boxShadow !== "none").toBeTruthy();

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

  const liveStyles = await page.evaluate(() => {
    const screen = window.getComputedStyle(document.querySelector(".screen-live") as HTMLElement);
    const stage = window.getComputedStyle(document.querySelector(".live-stage") as HTMLElement);
    const stream = window.getComputedStyle(document.querySelector(".stream-frame") as HTMLElement);
    const tray = window.getComputedStyle(document.querySelector(".live-control-tray") as HTMLElement);

    return {
      screenBoxShadow: screen.boxShadow,
      stageRadius: Number.parseFloat(stage.borderRadius),
      streamMinHeight: Number.parseFloat(stream.minHeight),
      trayRadius: Number.parseFloat(tray.borderRadius)
    };
  });
  expect(liveStyles.screenBoxShadow).toBe("none");
  expect(liveStyles.stageRadius).toBeGreaterThanOrEqual(32);
  expect(liveStyles.streamMinHeight).toBeGreaterThanOrEqual(480);
  expect(liveStyles.trayRadius).toBeGreaterThanOrEqual(26);
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
  await expect(page.getByText("2 of 2 captures shown.")).toBeVisible();
  await expect(page.getByLabel("Capture timeline days")).toContainText("Jun 25, 2026");
  await expect(page.getByLabel("Capture source filters")).toContainText("Scheduled");
  await expect(page.getByLabel("Capture source filters")).toContainText("Manual");
  await expect(page.getByLabel("Capture time filters")).toContainText("Afternoon");
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

  await page.getByLabel("Search captures").fill("manual");
  await expect(page.getByText("1 of 2 captures shown.")).toBeVisible();
  await expect(page.getByRole("button", { name: /20260625T195005491Z_manual.jpg/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /20260625T195000000Z_scheduled.jpg/ })).toBeHidden();
  await page.getByRole("button", { name: "All sources" }).click();
  await page.getByRole("button", { name: "Afternoon" }).click();
  await expect(page.getByText("1 of 2 captures shown.")).toBeVisible();
  await page.getByRole("button", { name: "Clear filter" }).click();
  await expect(page.getByText("2 of 2 captures shown.")).toBeVisible();

  await page.getByLabel("Filter captures by day").fill("2026-06-25");
  await expect(page.getByText(/2 of 2 captures shown for/)).toBeVisible();
  await page.getByRole("button", { name: "Clear filter" }).click();
  await expect(page.getByText("2 of 2 captures shown.")).toBeVisible();

  const galleryStyles = await page.evaluate(() => {
    const timelineChip = window.getComputedStyle(document.querySelector(".timeline-chip") as HTMLElement);
    const viewer = window.getComputedStyle(document.querySelector(".viewer-frame") as HTMLElement);
    const captureItem = window.getComputedStyle(document.querySelector(".capture-list-item.active") as HTMLElement);

    return {
      timelineChipRadius: Number.parseFloat(timelineChip.borderRadius),
      viewerMinHeight: Number.parseFloat(viewer.minHeight),
      captureItemShadow: captureItem.boxShadow
    };
  });
  expect(galleryStyles.timelineChipRadius).toBeGreaterThanOrEqual(16);
  expect(galleryStyles.viewerMinHeight).toBeGreaterThanOrEqual(360);
  expect(galleryStyles.captureItemShadow).toContain("rgb");
});

test("updates settings with discovered camera options", async ({ page }) => {
  await signIn(page);
  await navigateInApp(page, "/settings");

  await expect(page.getByRole("heading", { name: "Settings" })).toBeVisible();
  await expect(page.getByText("Found 2 cameras for Windows.")).toBeVisible();
  await expect(page.getByLabel("Settings summary")).toContainText("Logi C270 HD WebCam");
  await expect(page.getByLabel("Settings summary")).toContainText("1280x720");
  await expect(page.getByLabel("Camera resolution controls")).toBeVisible();
  await expect(page.getByLabel("Stream width")).toHaveValue("1280");
  await expect(page.getByLabel("Stream FPS")).toHaveValue("15");
  await page.getByLabel("Camera source selector").selectOption("@device_pnp_integrated");
  await expect(page.getByLabel("Selected camera source")).toHaveValue("@device_pnp_integrated");
  await page.getByLabel("Stream width").fill("1024");
  await page.getByLabel("Selected camera source").fill("Integrated Webcam");
  await page.getByLabel("Interval minutes").fill("10");
  await page.getByRole("button", { name: "Save settings" }).click();

  await expect(page.getByText("Settings saved. Refreshed values match the service response.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save settings" })).toBeDisabled();

  const settingsStyles = await page.evaluate(() => {
    const screen = window.getComputedStyle(document.querySelector(".screen-settings") as HTMLElement);
    const fieldset = window.getComputedStyle(document.querySelector(".settings-form fieldset") as HTMLElement);
    const saveButton = window.getComputedStyle(document.querySelector(".settings-form .primary-button") as HTMLElement);

    return {
      screenBackground: screen.backgroundImage,
      fieldsetPaddingLeft: Number.parseFloat(fieldset.paddingLeft),
      saveButtonBackground: saveButton.backgroundImage,
      saveButtonRadius: Number.parseFloat(saveButton.borderRadius)
    };
  });
  expect(settingsStyles.screenBackground).toContain("gradient");
  expect(settingsStyles.fieldsetPaddingLeft).toBeGreaterThanOrEqual(16);
  expect(settingsStyles.saveButtonBackground).toContain("gradient");
  expect(settingsStyles.saveButtonRadius).toBeGreaterThanOrEqual(30);
});
