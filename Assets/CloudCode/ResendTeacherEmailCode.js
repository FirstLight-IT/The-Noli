const { DataApi } = require("@unity-services/cloud-save-1.4");

// This endpoint has no client parameters.
const axios = require("axios-1.6");
const _ = require("lodash-4.17");

const PROFILE_KEY = "account_profile";
const EMAIL_VERIFICATION_KEY = "teacher_email_verification";
const CODE_LIFETIME_MINUTES = 15;
const RESEND_COOLDOWN_SECONDS = 60;
const MAX_RESENDS = 3;
const RESEND_WINDOW_HOURS = 24;

module.exports = async ({ context, logger, secretManager }) => {
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

  if (profile.teacherVerificationStatus !== "AwaitingEmailConfirmation") {
    return failed("This Teacher account is not awaiting email confirmation.");
  }

  const verification = await loadValue(
    cloudSave,
    context.projectId,
    context.playerId,
    EMAIL_VERIFICATION_KEY
  );

  const now = new Date();
  let resendCount = Number(verification?.resendCount || 0);
  let resendWindowStartedAt = Date.parse(
    verification?.resendWindowStartedAtUtc || ""
  );
  const resendWindowMilliseconds = RESEND_WINDOW_HOURS * 60 * 60 * 1000;

  if (
    !Number.isFinite(resendWindowStartedAt) ||
    now.getTime() - resendWindowStartedAt >= resendWindowMilliseconds
  ) {
    resendCount = 0;
    resendWindowStartedAt = now.getTime();
  }

  if (resendCount >= MAX_RESENDS) {
    const hoursRemaining = Math.max(
      1,
      Math.ceil(
        (resendWindowStartedAt + resendWindowMilliseconds - now.getTime()) /
        (60 * 60 * 1000)
      )
    );
    return failed(
      `The resend limit was reached. Try again in about ${hoursRemaining} hour(s).`
    );
  }

  const lastSentAt = Date.parse(verification?.lastSentAtUtc || "");
  const elapsedSeconds = Number.isFinite(lastSentAt)
    ? Math.floor((Date.now() - lastSentAt) / 1000)
    : RESEND_COOLDOWN_SECONDS;

  if (elapsedSeconds < RESEND_COOLDOWN_SECONDS) {
    return failed(
      `Please wait ${RESEND_COOLDOWN_SECONDS - elapsedSeconds} seconds before resending.`
    );
  }

  const code = _.random(0, 999999).toString().padStart(6, "0");
  const updatedVerification = {
    code,
    expiresAtUtc: new Date(
      now.getTime() + CODE_LIFETIME_MINUTES * 60 * 1000
    ).toISOString(),
    attemptsRemaining: 5,
    resendCount: resendCount + 1,
    resendWindowStartedAtUtc: new Date(resendWindowStartedAt).toISOString(),
    lastSentAtUtc: now.toISOString(),
    status: "Pending"
  };

  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: EMAIL_VERIFICATION_KEY,
    value: updatedVerification
  });

  await sendVerificationEmail(
    secretManager,
    profile.schoolEmail,
    profile.fullName,
    code
  );

  logger.info("Resent a Teacher email confirmation code.", {
    playerId: context.playerId,
    resendCount: updatedVerification.resendCount
  });

  return {
    success: true,
    error: "",
    expiresAtUtc: updatedVerification.expiresAtUtc,
    resendsRemaining: MAX_RESENDS - updatedVerification.resendCount
  };
};

function failed(error) {
  return {
    success: false,
    error,
    expiresAtUtc: "",
    resendsRemaining: 0
  };
}

async function loadValue(cloudSave, projectId, playerId, key) {
  const response = await cloudSave.getProtectedItems(projectId, playerId, [key]);
  return response.data.results.length > 0
    ? response.data.results[0].value
    : null;
}

async function sendVerificationEmail(secretManager, email, teacherName, code) {
  const [urlSecret, authSecret] = await Promise.all([
    secretManager.getSecret("NOLI_EMAIL_SERVICE_URL"),
    secretManager.getSecret("NOLI_EMAIL_SERVICE_SECRET")
  ]);

  const response = await axios.post(
    urlSecret.value,
    {
      secret: authSecret.value,
      email,
      teacherName,
      code
    },
    {
      headers: { "Content-Type": "application/json" },
      timeout: 15000
    }
  );

  const result = typeof response.data === "string"
    ? JSON.parse(response.data)
    : response.data;

  if (!result || result.success !== true) {
    throw new Error(result?.message || "The verification email could not be sent.");
  }
}
