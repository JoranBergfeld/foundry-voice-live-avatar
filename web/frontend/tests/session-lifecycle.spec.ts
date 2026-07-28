import { expect, test } from "@playwright/test";
import {
  closeLatestSocketCleanly,
  closeLatestSocketUnexpectedly,
  controlFrames,
  deferNextCreateOffer,
  deferNextResume,
  failNext,
  inspectLifecycle,
  installBrowserMocks,
  sendReadyFrame,
  sendServerFrame,
  settleCreateOffer,
  settleResume,
} from "./browser-mocks";

test.beforeEach(async ({ page }) => {
  await installBrowserMocks(page);
});

async function openOperator(page: Parameters<typeof installBrowserMocks>[0]) {
  await page.goto("/?view=operator");
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(1);
}

async function readyOperator(page: Parameters<typeof installBrowserMocks>[0], activeMode: "gated" | "open-mic" | "hybrid" = "gated") {
  await sendReadyFrame(page, { activeMode });
  await expect(page.getByText("microphone: ready (24000 Hz context)")).toBeVisible();
  if (activeMode !== "gated") {
    await expect(page.getByText(`turn: ${activeMode}: streaming continuously`)).toBeVisible();
  }
}

async function openDisplay(page: Parameters<typeof installBrowserMocks>[0]) {
  await page.goto("/?view=display");
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(1);
}

async function readyDisplay(page: Parameters<typeof installBrowserMocks>[0]) {
  await sendReadyFrame(page);
  await expect.poll(async () => (await inspectLifecycle(page)).peerConnections.at(-1)?.offerCalls).toBe(1);
  await expect(page.getByText("webrtc: offer sent; waiting for answer")).toBeVisible();
}

test("display reconnects with fresh resources after clean socket closure", async ({ page }) => {
  await openDisplay(page);
  await readyDisplay(page);

  await closeLatestSocketCleanly(page);
  const reconnect = page.getByRole("button", { name: "Reconnect" });
  await expect(reconnect).toBeVisible();

  await reconnect.click();
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(2);
  await readyDisplay(page);

  const state = await inspectLifecycle(page);
  expect(state.sockets).toHaveLength(2);
  expect(state.peerConnections).toHaveLength(2);
  expect(state.peerConnections[0]).toMatchObject({ closed: true, closeCalls: 1 });
  expect(state.peerConnections[1].closed).toBe(false);
  expect(state.getUserMediaCalls).toBe(0);
  expect(state.audioContexts).toHaveLength(0);
  await expect(reconnect).toBeHidden();
});

test("display unexpected closure retains the error and offers reconnect", async ({ page }) => {
  await openDisplay(page);
  await readyDisplay(page);

  await closeLatestSocketUnexpectedly(page);

  await expect(page.getByRole("alert")).toContainText("WebSocket closed unexpectedly");
  await expect(page.getByRole("button", { name: "Reconnect" })).toBeVisible();
});

test("socket closure tears down browser resources and offers reconnect", async ({ page }) => {
  await openOperator(page);
  await readyOperator(page);

  await closeLatestSocketCleanly(page);

  await expect(page.getByRole("button", { name: "Reconnect" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Hold to talk" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Stop speaking" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Repeat last answer" })).toBeDisabled();
  await expect.poll(async () => (await inspectLifecycle(page)).audioContexts[0]?.state).toBe("closed");
  const state = await inspectLifecycle(page);
  expect(state.streams[0].stoppedTracks).toBe(1);
  expect(state.audioContexts[0]).toMatchObject({ sampleRate: 24_000, closeCalls: 1 });
  expect(state.peerConnections[0]).toMatchObject({ closed: true, closeCalls: 1 });
  expect(state.currentSocketIds).toEqual([]);
});

test("clean closure is nonfatal while unexpected closure reports an error", async ({ page }) => {
  await openOperator(page);
  await readyOperator(page);
  await closeLatestSocketCleanly(page);
  await expect(page.getByRole("alert")).toBeHidden();

  await page.getByRole("button", { name: "Reconnect" }).click();
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(2);
  await readyOperator(page);
  await closeLatestSocketUnexpectedly(page);
  await expect(page.getByRole("alert")).toContainText("WebSocket closed unexpectedly");
});

test("reconnect creates fresh resources and can become ready again", async ({ page }) => {
  await openOperator(page);
  await readyOperator(page);
  await closeLatestSocketCleanly(page);

  await page.getByRole("button", { name: "Reconnect" }).click();
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(2);
  await readyOperator(page, "open-mic");

  const state = await inspectLifecycle(page);
  expect(state.sockets).toHaveLength(2);
  expect(state.streams).toHaveLength(2);
  expect(state.audioContexts).toHaveLength(2);
  expect(state.peerConnections).toHaveLength(2);
  expect(state.streams[0].stoppedTracks).toBe(1);
  expect(state.audioContexts[0].state).toBe("closed");
  expect(state.audioContexts[1].state).toBe("running");
  await expect(page.getByText("connection: ready")).toBeVisible();
});

test("microphone setup failure tears down its track and offers reconnect", async ({ page }) => {
  await openOperator(page);
  await failNext(page, "audioWorklet", "worklet failed");
  await sendReadyFrame(page);

  await expect(page.getByRole("alert")).toContainText("Microphone setup failed: worklet failed");
  await expect(page.getByRole("button", { name: "Reconnect" })).toBeVisible();
  await expect.poll(async () => (await inspectLifecycle(page)).streams[0]?.stoppedTracks).toBe(1);
  const state = await inspectLifecycle(page);
  expect(state.audioContexts[0].state).toBe("closed");
  expect(state.peerConnections[0].closed).toBe(true);
});

test("avatar negotiation failure tears down and offers reconnect", async ({ page }) => {
  await openOperator(page);
  await deferNextCreateOffer(page);
  await sendReadyFrame(page);
  await expect.poll(async () => (await inspectLifecycle(page)).peerConnections[0]?.offerCalls).toBe(1);
  await settleCreateOffer(page, 1, "offer failed");

  await expect(page.getByRole("alert")).toContainText("Avatar WebRTC negotiation failed: offer failed");
  await expect(page.getByRole("button", { name: "Reconnect" })).toBeVisible();
  const state = await inspectLifecycle(page);
  expect(state.peerConnections[0].closed).toBe(true);
  expect(state.streams).toHaveLength(0);
  expect(state.audioContexts).toHaveLength(0);
});

test("avatar capacity error keeps the voice session connected", async ({ page }) => {
  await openOperator(page);
  await readyOperator(page);
  await sendServerFrame(page, { t: "avatar-error", code: "capacity", message: "No avatar capacity" });

  await expect(page.getByRole("status")).toContainText("Avatar unavailable: No avatar capacity");
  await expect(page.getByText("connection: ready")).toBeVisible();
  await expect(page.getByRole("button", { name: "Reconnect" })).toBeHidden();
  await expect(page.getByRole("alert")).toBeHidden();
  const state = await inspectLifecycle(page);
  expect(state.peerConnections[0].closed).toBe(true);
  expect(state.audioContexts[0].state).not.toBe("closed");
  expect(state.streams[0].stoppedTracks).toBe(0);
});

test("releasing a gated turn while resume is pending never starts it", async ({ page }) => {
  await openOperator(page);
  await readyOperator(page);
  await deferNextResume(page);

  const hold = page.getByRole("button", { name: "Hold to talk" });
  await hold.dispatchEvent("pointerdown");
  await expect.poll(async () => (await inspectLifecycle(page)).audioContexts[0].resumeCalls).toBe(1);
  await hold.dispatchEvent("pointerup");
  await settleResume(page, 1);

  await expect.poll(async () => controlFrames(await inspectLifecycle(page)).filter((frame) => frame.t === "start-turn").length).toBe(0);
  await expect(hold).not.toHaveClass(/active/);
});

test("stale gated resume rejection cannot overwrite a reconnected session", async ({ page }) => {
  await openOperator(page);
  await readyOperator(page);
  await deferNextResume(page);
  await page.getByRole("button", { name: "Hold to talk" }).dispatchEvent("pointerdown");
  await expect.poll(async () => (await inspectLifecycle(page)).audioContexts[0].resumeCalls).toBe(1);

  await closeLatestSocketUnexpectedly(page);
  await page.getByRole("button", { name: "Reconnect" }).click();
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(2);
  await readyOperator(page);
  await settleResume(page, 1, "stale resume failed");

  await expect(page.getByRole("alert")).toBeHidden();
  await expect(page.getByText("connection: ready")).toBeVisible();
  await expect(page.getByRole("button", { name: "Reconnect" })).toBeHidden();
});

test("landing controls are labeled and visible above the avatar", async ({ page }) => {
  for (const viewport of [
    { width: 1280, height: 720 },
    { width: 390, height: 844 },
  ]) {
    await page.setViewportSize(viewport);
    await page.goto("/");

    const actions = page.getByRole("navigation", { name: "Landing controls" });
    const config = page.getByRole("link", { name: "Config" });
    const transcript = page.getByRole("button", { name: "Transcript" });

    await expect(actions).toBeVisible();
    await expect(actions).toHaveCSS("z-index", "3");
    await expect(config).toBeVisible();
    await expect(config).toHaveAttribute("href", "?view=operator");
    await expect(transcript).toBeVisible();

    const box = await actions.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.y).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width);
    expect(box!.y + box!.height).toBeLessThanOrEqual(viewport.height);

    await transcript.click();
    await expect(page.locator(".landing-transcript")).toHaveClass(/open/);
  }
});

test("transcript close panel button clears the open class on desktop 1280x720", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.goto("/");

  await page.getByRole("button", { name: "Transcript" }).click();
  await expect(page.locator(".landing-transcript")).toHaveClass(/open/);

  // Close button must not be geometrically covered by the toolbar
  const toolbarBox = await page.getByRole("navigation", { name: "Landing controls" }).boundingBox();
  const closeBox = await page.getByRole("button", { name: "Close panel" }).boundingBox();
  expect(toolbarBox).not.toBeNull();
  expect(closeBox).not.toBeNull();
  expect(closeBox!.y).toBeGreaterThanOrEqual(toolbarBox!.y + toolbarBox!.height);

  // Clicking the button must actually close the panel
  await page.getByRole("button", { name: "Close panel" }).click();
  await expect(page.locator(".landing-transcript")).not.toHaveClass(/open/);
});

test("pill and notice do not overlap the toolbar on mobile 390x844", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");

  await expect(page.getByRole("navigation", { name: "Landing controls" })).toBeVisible();
  const toolbarBox = await page.getByRole("navigation", { name: "Landing controls" }).boundingBox();
  expect(toolbarBox).not.toBeNull();

  // Reveal pill and notice via DOM manipulation for layout testing
  await page.evaluate(() => {
    const pill = document.querySelector<HTMLElement>(".landing-pill");
    const notice = document.querySelector<HTMLElement>(".landing-notice");
    if (pill) { pill.hidden = false; pill.textContent = "Connecting\u2026"; }
    if (notice) { notice.hidden = false; notice.textContent = "Non-fatal notice"; }
  });

  const pillBox = await page.locator(".landing-pill").boundingBox();
  const noticeBox = await page.locator(".landing-notice").boundingBox();
  expect(pillBox).not.toBeNull();
  expect(noticeBox).not.toBeNull();

  const toolbarBottom = toolbarBox!.y + toolbarBox!.height;
  // Both elements must start at or below the toolbar bottom edge
  expect(pillBox!.y).toBeGreaterThanOrEqual(toolbarBottom);
  expect(noticeBox!.y).toBeGreaterThanOrEqual(toolbarBottom);
});

test("reconnect with changed mode replaces old gated and mute handlers", async ({ page }) => {
  await page.goto("/");
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(1);
  await sendReadyFrame(page, { activeMode: "gated" });
  await expect.poll(async () => (await inspectLifecycle(page)).audioContexts.length).toBe(1);
  const talk = page.locator(".landing-talk");
  await expect(talk).toBeEnabled();

  await closeLatestSocketCleanly(page);
  await page.getByRole("button", { name: "Reconnect" }).click();
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(2);
  await sendReadyFrame(page, { activeMode: "open-mic" });
  await expect.poll(async () => (await inspectLifecycle(page)).audioContexts[1]?.state).toBe("running");
  await expect(talk).toContainText("Listening");
  await talk.dispatchEvent("pointerdown");
  expect(controlFrames(await inspectLifecycle(page), 2).filter((frame) => frame.t === "start-turn")).toHaveLength(0);
  await talk.dispatchEvent("click");
  await expect(talk).toContainText("Muted");

  await closeLatestSocketCleanly(page);
  await page.getByRole("button", { name: "Reconnect" }).click();
  await expect.poll(async () => (await inspectLifecycle(page)).sockets.length).toBe(3);
  await sendReadyFrame(page, { activeMode: "gated" });
  await expect.poll(async () => (await inspectLifecycle(page)).audioContexts.length).toBe(3);
  await expect(talk).toContainText("Hold to talk");
  await talk.dispatchEvent("click");
  await expect(talk).not.toContainText("Muted");
  expect(controlFrames(await inspectLifecycle(page), 3).filter((frame) => frame.t === "start-turn")).toHaveLength(0);
  await talk.dispatchEvent("pointerdown");
  await expect.poll(async () => controlFrames(await inspectLifecycle(page), 3).filter((frame) => frame.t === "start-turn").length).toBe(1);
});
