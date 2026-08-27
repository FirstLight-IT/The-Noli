const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const OWNED_ROOMS_KEY = "teacher_owned_classroom_ids";
const ROOM_KEY = "room";
const CODE_KEY = "room_id";
const CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
const MAX_OWNED_CLASSROOMS = 4;

module.exports = async ({ params, context, logger }) => {
  const roomName = clean(params.roomName);
  if (roomName.length < 3 || roomName.length > 50) {
    throw new Error("Classroom name must be between 3 and 50 characters.");
  }

  const cloudSave = new DataApi(context);
  const profile = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    PROFILE_KEY
  );

  if (!profile ||
      profile.role !== "Teacher" ||
      profile.teacherVerificationStatus !== "Verified") {
    throw new Error("Only a verified Teacher may create a classroom.");
  }

  const ownedRoomIds = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    OWNED_ROOMS_KEY
  );
  const existingRoomIds = Array.isArray(ownedRoomIds) ? ownedRoomIds.slice() : [];
  let ownedClassroomCount = 0;
  for (const ownedRoomId of existingRoomIds) {
    const existingRoom = await loadPrivateCustomValue(
      cloudSave, context.projectId, roomEntityId(ownedRoomId), ROOM_KEY
    );
    if (existingRoom && existingRoom.teacherAccountId === context.playerId &&
        existingRoom.status !== "Deleted" && existingRoom.status !== "Archived") {
      ownedClassroomCount++;
    }
  }
  if (ownedClassroomCount >= MAX_OWNED_CLASSROOMS) {
    throw new Error("A Teacher may own at most four classrooms. Delete one before creating another.");
  }

  const roomId = createRoomId(context.playerId);
  const joinCode = await createUniqueJoinCode(cloudSave, context.projectId);
  const createdAtUtc = new Date().toISOString();
  const room = {
    roomId,
    roomName,
    joinCode,
    teacherAccountId: context.playerId,
    teacherInGameName: clean(profile.inGameName),
    status: "Active",
    createdAtUtc,
    members: []
  };

  await cloudSave.setPrivateCustomItem(context.projectId, roomEntityId(roomId), {
    key: ROOM_KEY,
    value: room
  });

  await cloudSave.setPrivateCustomItem(context.projectId, codeEntityId(joinCode), {
    key: CODE_KEY,
    value: roomId
  });

  const updatedRoomIds = existingRoomIds;
  if (!updatedRoomIds.includes(roomId)) {
    updatedRoomIds.push(roomId);
  }

  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: OWNED_ROOMS_KEY,
    value: updatedRoomIds
  });

  logger.info("Created a classroom.", {
    roomId,
    teacherAccountId: context.playerId
  });

  return {
    roomId,
    roomName,
    joinCode,
    status: room.status,
    createdAtUtc,
    memberCount: 0
  };
};

async function loadProtectedValue(cloudSave, projectId, playerId, key) {
  const response = await cloudSave.getProtectedItems(projectId, playerId, [key]);
  const item = response.data.results.find(result => result.key === key);
  return item ? item.value : null;
}

async function createUniqueJoinCode(cloudSave, projectId) {
  for (let attempt = 0; attempt < 10; attempt++) {
    let code = "";
    for (let index = 0; index < 6; index++) {
      code += CODE_ALPHABET[Math.floor(Math.random() * CODE_ALPHABET.length)];
    }

    const existing = await cloudSave.getPrivateCustomItems(
      projectId,
      codeEntityId(code)
    );
    if (!existing.data.results || existing.data.results.length === 0) {
      return code;
    }
  }

  throw new Error("A unique classroom code could not be generated. Please try again.");
}

async function loadPrivateCustomValue(cloudSave, projectId, customId, key) {
  const response = await cloudSave.getPrivateCustomItems(projectId, customId);
  const item = response.data.results.find(result => result.key === key);
  return item ? item.value : null;
}

function createRoomId(playerId) {
  const suffix = Math.floor(Math.random() * 0x100000000).toString(36);
  return `${playerId}_${Date.now().toString(36)}_${suffix}`;
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
  roomName: { type: "String", required: true }
};
