const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const TEACHER_REQUEST_STATUS_KEY = "teacher_request_status";

module.exports = async ({ params, context, logger }) => {
  const username = clean(params.username);
  const inGameName = clean(params.inGameName);
  const requestedRole = clean(params.requestedRole).toLowerCase();
  const fullName = clean(params.fullName);
  const schoolEmail = clean(params.schoolEmail).toLowerCase();

  if (!username || !inGameName) {
    throw new Error("Username and in-game name are required.");
  }

  if (requestedRole !== "player" && requestedRole !== "teacher") {
    throw new Error("Only Player or Teacher registration is allowed.");
  }

  if (requestedRole === "teacher") {
    if (!fullName) {
      throw new Error("A full name is required for Teacher registration.");
    }

    if (!isValidEmail(schoolEmail)) {
      throw new Error("Teacher registration requires a valid email address.");
    }
  }

  const cloudSave = new DataApi(context);
  const existing = await cloudSave.getProtectedItems(
    context.projectId,
    context.playerId,
    [PROFILE_KEY]
  );

  if (existing.data.results.length > 0) {
    return existing.data.results[0].value;
  }

  const profile = {
    accountId: context.playerId,
    username,
    inGameName,
    role: requestedRole === "teacher" ? "Teacher" : "Player",
    teacherVerificationStatus:
      requestedRole === "teacher" ? "Pending" : "NotApplicable",
    effectiveRole: "Player",
    fullName: requestedRole === "teacher" ? fullName : "",
    schoolEmail: requestedRole === "teacher" ? schoolEmail : "",
    emailConfirmationStatus:
      requestedRole === "teacher" ? "NotRequested" : "NotApplicable",
    createdAtUtc: new Date().toISOString()
  };

  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: PROFILE_KEY,
    value: profile
  });

  if (requestedRole === "teacher") {
    await cloudSave.setProtectedItem(context.projectId, context.playerId, {
      key: TEACHER_REQUEST_STATUS_KEY,
      value: "Pending"
    });
  }

  logger.info("Created The Noli account profile.", {
    playerId: context.playerId,
    requestedRole
  });

  return profile;
};

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
}

function isValidEmail(value) {
  return /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value);
}
