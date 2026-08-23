const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const TEACHER_REQUEST_STATUS_KEY = "teacher_request_status";
const EMAIL_VERIFICATION_KEY = "teacher_email_verification";

module.exports = async ({ params, context, logger }) => {
  const code = clean(params.code);
  if (!/^\d{6}$/.test(code)) {
    return failed("Enter the six-digit confirmation code.");
  }

  const cloudSave = new DataApi(context);
  const profile = await loadValue(
    cloudSave,
    context.projectId,
    context.playerId,
    PROFILE_KEY
  );

  if (!profile || profile.role !== "Teacher") {
    return failed("This account does not have a Teacher request.");
  }

  if (profile.teacherVerificationStatus === "Verified") {
    return succeeded(profile);
  }

  if (profile.teacherVerificationStatus !== "AwaitingEmailConfirmation") {
    return failed("This Teacher request is not awaiting email confirmation.");
  }

  const verification = await loadValue(
    cloudSave,
    context.projectId,
    context.playerId,
    EMAIL_VERIFICATION_KEY
  );

  if (!verification) {
    return failed("No active confirmation code was found.");
  }

  if (verification.status === "Expired") {
    return failed("That confirmation code has expired. Request a new code.");
  }

  if (verification.status !== "Pending") {
    return failed("This confirmation code is no longer active. Request a new code.");
  }

  if (Date.now() > Date.parse(verification.expiresAtUtc)) {
    verification.status = "Expired";
    await saveVerification(cloudSave, context, verification);
    return failed("That confirmation code has expired.");
  }

  if (verification.attemptsRemaining <= 0) {
    return failed("Too many incorrect attempts. Request a new code.");
  }

  if (code !== verification.code) {
    verification.attemptsRemaining -= 1;
    await saveVerification(cloudSave, context, verification);
    return failed(
      `Incorrect confirmation code. ${verification.attemptsRemaining} attempts remain.`
    );
  }

  verification.status = "Confirmed";
  verification.code = "";
  verification.attemptsRemaining = 0;

  profile.teacherVerificationStatus = "Verified";
  profile.emailConfirmationStatus = "Confirmed";
  profile.effectiveRole = "Teacher";

  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: PROFILE_KEY,
    value: profile
  });
  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: TEACHER_REQUEST_STATUS_KEY,
    value: "Verified"
  });
  await saveVerification(cloudSave, context, verification);

  logger.info("Confirmed a Teacher school email.", {
    playerId: context.playerId
  });

  return succeeded(profile);
};

function succeeded(profile) {
  return { success: true, error: "", profile };
}

function failed(error) {
  return { success: false, error, profile: null };
}

async function loadValue(cloudSave, projectId, playerId, key) {
  const response = await cloudSave.getProtectedItems(projectId, playerId, [key]);
  return response.data.results.length > 0
    ? response.data.results[0].value
    : null;
}

async function saveVerification(cloudSave, context, value) {
  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: EMAIL_VERIFICATION_KEY,
    value
  });
}

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
}
