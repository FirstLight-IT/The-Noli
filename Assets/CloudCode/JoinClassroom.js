const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const MEMBERSHIPS_KEY = "classroom_memberships";
const ROOM_KEY = "room";
const CODE_KEY = "room_id";
const MAX_ACTIVE_CLASSROOMS = 3;

module.exports = async ({ params, context, logger }) => {
  const joinCode = clean(params.joinCode).toUpperCase();
  if (!/^[A-Z2-9]{6}$/.test(joinCode)) {
    return failed("Enter a valid six-character classroom code.");
  }

  const cloudSave = new DataApi(context);
  const profile = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    PROFILE_KEY
  );
  if (!profile) {
    return failed("A complete The Noli account profile is required.");
  }

  const isVerifiedTeacher = profile.role === "Teacher" &&
    profile.teacherVerificationStatus === "Verified";
  if (profile.role === "Librarian" || isVerifiedTeacher) {
    return failed("Only accounts with Player access may join classrooms.");
  }

  const roomId = await loadPrivateCustomValue(
    cloudSave,
    context.projectId,
    codeEntityId(joinCode),
    CODE_KEY
  );
  if (!roomId) {
    return failed("No classroom was found for that code.");
  }

  const room = await loadPrivateCustomValue(
    cloudSave,
    context.projectId,
    roomEntityId(roomId),
    ROOM_KEY
  );
  if (!room || room.status !== "Active") {
    return failed("This classroom is not currently accepting members.");
  }

  const savedMemberships = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    MEMBERSHIPS_KEY
  );
  const memberships = Array.isArray(savedMemberships) ? savedMemberships.slice() : [];
  const existingMembership = memberships.find(item => item.roomId === roomId);

  if (!existingMembership) {
    const activeCount = memberships.filter(item => item.status === "Active").length;
    if (activeCount >= MAX_ACTIVE_CLASSROOMS) {
      return failed("You are already in the maximum of three active classrooms.");
    }
  }

  const now = new Date().toISOString();
  const membership = existingMembership || {
    roomId,
    roomName: room.roomName,
    teacherAccountId: room.teacherAccountId,
    teacherInGameName: room.teacherInGameName,
    joinedAtUtc: now
  };
  membership.roomName = room.roomName;
  membership.teacherInGameName = room.teacherInGameName;
  membership.status = "Active";
  membership.leftAtUtc = "";
  if (!existingMembership) {
    memberships.push(membership);
  }

  room.members = Array.isArray(room.members) ? room.members : [];
  let member = room.members.find(item => item.accountId === context.playerId);
  if (!member) {
    member = {
      accountId: context.playerId,
      inGameName: clean(profile.inGameName),
      joinedAtUtc: now
    };
    room.members.push(member);
  }
  member.inGameName = clean(profile.inGameName);
  member.status = "Active";
  member.leftAtUtc = "";

  await cloudSave.setPrivateCustomItem(context.projectId, roomEntityId(roomId), {
    key: ROOM_KEY,
    value: room
  });
  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: MEMBERSHIPS_KEY,
    value: memberships
  });

  logger.info("Player joined a classroom.", {
    roomId,
    playerId: context.playerId
  });

  return {
    success: true,
    error: "",
    membership: {
      roomId,
      roomName: room.roomName,
      teacherAccountId: room.teacherAccountId,
      teacherInGameName: room.teacherInGameName,
      status: membership.status,
      joinedAtUtc: membership.joinedAtUtc,
      leftAtUtc: ""
    }
  };
};

function failed(error) {
  return { success: false, error, membership: null };
}

async function loadProtectedValue(cloudSave, projectId, playerId, key) {
  const response = await cloudSave.getProtectedItems(projectId, playerId, [key]);
  const item = response.data.results.find(result => result.key === key);
  return item ? item.value : null;
}

async function loadPrivateCustomValue(cloudSave, projectId, customId, key) {
  const response = await cloudSave.getPrivateCustomItems(projectId, customId);
  const item = response.data.results.find(result => result.key === key);
  return item ? item.value : null;
}

function roomEntityId(roomId) {
  return `classroom_${roomId}`;
}

function codeEntityId(code) {
  return `classroom_code_${code}`;
}

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
}

module.exports.params = {
  joinCode: { type: "String", required: true }
};
