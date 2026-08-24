const { DataApi } = require("@unity-services/cloud-save-1.4");
const axios = require("axios-1.6");
const _ = require("lodash-4.17");

const PROFILE_KEY = "account_profile";
const TEACHER_REQUEST_STATUS_KEY = "teacher_request_status";
const EMAIL_VERIFICATION_KEY = "teacher_email_verification";
const CODE_LIFETIME_MINUTES = 15;

module.exports = async ({ params, context, logger, secretManager }) => {
  const targetAccountId = clean(params.targetAccountId);
  const decision = clean(params.decision).toLowerCase();

  if (!targetAccountId) {
    throw new Error("A Teacher Account ID is required.");
  }

  if (decision !== "approve" && decision !== "reject") {
    throw new Error("The decision must be approve or reject.");
  }

  if (targetAccountId === context.playerId) {
    throw new Error("A Librarian cannot review their own account.");
  }

  const cloudSave = new DataApi(context);
  const librarianProfile = await loadProfile(
    cloudSave,
    context.projectId,
    context.playerId
  );

  if (
    !librarianProfile ||
    librarianProfile.role !== "Librarian" ||
    librarianProfile.effectiveRole !== "Librarian"
  ) {
    throw new Error("Only a verified Librarian may review Teacher accounts.");
  }

  const teacherProfile = await loadProfile(
    cloudSave,
    context.projectId,
    targetAccountId
  );

  if (!teacherProfile || teacherProfile.role !== "Teacher") {
    throw new Error("The selected account is not a Teacher request.");
  }

  if (teacherProfile.teacherVerificationStatus !== "Pending") {
    throw new Error("This Teacher request has already been reviewed.");
  }

  if (decision === "approve") {
    const code = _.random(0, 999999).toString().padStart(6, "0");
    const expiresAtUtc = new Date(
      Date.now() + CODE_LIFETIME_MINUTES * 60 * 1000
    ).toISOString();

    await cloudSave.setProtectedItem(context.projectId, targetAccountId, {
      key: EMAIL_VERIFICATION_KEY,
      value: {
        code,
        expiresAtUtc,
        attemptsRemaining: 5,
        resendCount: 0,
        resendWindowStartedAtUtc: new Date().toISOString(),
        lastSentAtUtc: new Date().toISOString(),
        status: "Pending"
      }
    });

    await sendVerificationEmail(
      secretManager,
      teacherProfile.schoolEmail,
      teacherProfile.fullName,
      code
    );

    teacherProfile.teacherVerificationStatus = "AwaitingEmailConfirmation";
    teacherProfile.emailConfirmationStatus = "Pending";
    teacherProfile.effectiveRole = "Player";
  } else {
    teacherProfile.teacherVerificationStatus = "Rejected";
    teacherProfile.emailConfirmationStatus = "NotRequested";
    teacherProfile.effectiveRole = "Player";
  }

  await cloudSave.setProtectedItem(context.projectId, targetAccountId, {
    key: PROFILE_KEY,
    value: teacherProfile
  });
  await cloudSave.setProtectedItem(context.projectId, targetAccountId, {
    key: TEACHER_REQUEST_STATUS_KEY,
    value: teacherProfile.teacherVerificationStatus
  });

  logger.info("Reviewed a Teacher account request.", {
    librarianPlayerId: context.playerId,
    targetAccountId,
    decision
  });

  return {
    targetAccountId,
    decision,
    teacherVerificationStatus: teacherProfile.teacherVerificationStatus,
    emailConfirmationStatus: teacherProfile.emailConfirmationStatus
  };
};

async function loadProfile(cloudSave, projectId, playerId) {
  const response = await cloudSave.getProtectedItems(
    projectId,
    playerId,
    [PROFILE_KEY]
  );

  return response.data.results.length > 0
    ? response.data.results[0].value
    : null;
}

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
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

module.exports.params = {
  targetAccountId: { type: "String", required: true },
  decision: { type: "String", required: true }
};
