const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";

module.exports = async ({ params, context, logger }) => {
  const inGameName = clean(params.inGameName);

  if (!inGameName || inGameName.length > 30) {
    throw new Error("In-game name must be 1 to 30 characters.");
  }

  const cloudSave = new DataApi(context);
  const existing = await cloudSave.getProtectedItems(
    context.projectId,
    context.playerId,
    [PROFILE_KEY]
  );

  if (existing.data.results.length === 0) {
    throw new Error("The signed-in account profile could not be found.");
  }

  const profile = existing.data.results[0].value;
  if (profile.accountId !== context.playerId) {
    throw new Error("The account profile ownership check failed.");
  }

  profile.inGameName = inGameName;

  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: PROFILE_KEY,
    value: profile
  });

  logger.info("Updated The Noli in-game name.", {
    playerId: context.playerId
  });

  return profile;
};

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
}
