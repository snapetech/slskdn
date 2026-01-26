import { expect, Page, APIRequestContext } from "@playwright/test";
import { NodeCfg } from "./env";
import { T } from "./selectors";

export async function waitForHealth(request: APIRequestContext, baseUrl: string) {
  const health = `${baseUrl}/health`;
  for (let i = 0; i < 120; i++) {
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error(`Timed out waiting for ${health}`);
}

export async function login(page: Page, node: NodeCfg) {
  await page.goto(node.baseUrl, { waitUntil: "domcontentloaded" });

  const user = page.getByTestId(T.loginUsername);
  const pass = page.getByTestId(T.loginPassword);
  const submit = page.getByTestId(T.loginSubmit);

  await expect(user).toBeVisible();
  await user.fill(node.username);
  await pass.fill(node.password);
  await submit.click();

  // A stable post-login condition: sidebar/system link present
  await expect(page.getByTestId(T.navSystem)).toBeVisible();
}

export async function goto(page: Page, node: NodeCfg, route: string) {
  await page.goto(`${node.baseUrl}${route}`, { waitUntil: "domcontentloaded" });
}

export async function clickNav(page: Page, testId: string) {
  const el = page.getByTestId(testId);
  await expect(el).toBeVisible();
  await el.click();
}
