const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const MEMBERSHIPS_KEY = "classroom_memberships";
const ROOM_KEY = "room";

module.exports = async ({ context, logger }) => {
  const cloudSave = new DataApi(context);
  const profile = await loadProtectedValue(
    cloudSave, context.projectId, context.playerId, PROFILE_KEY
  );

  const isVerifiedTeacher = profile && profile.role === "Teacher" &&
    profile.teacherVerificationStatus === "Verified";
  if (!profile || profile.role === "Librarian" || isVerifiedTeacher) {
    throw new Error("Player access is required to view joined classrooms.");
  }

  const savedMemberships = await loadProtectedValue(
    cloudSave, context.projectId, context.playerId, MEMBERSHIPS_KEY
  );
  const candidates = Array.isArray(savedMemberships)
    ? savedMemberships.filter(item => item && item.status === "Active")
    : [];
  const memberships = [];
  const deletedRoomIds = [];
  for (const membership of candidates) {
    const room = await loadPrivateCustomValue(
      cloudSave, context.projectId, roomEntityId(membership.roomId), ROOM_KEY
    );
    if (room && room.status === "Active") {
      membership.roomName = room.roomName;
      membership.teacherInGameName = room.teacherInGameName;
      membership.roomStatus = room.status;
      memberships.push(membership);
    } else if (room && (room.status === "Deleted" || room.status === "Archived")) {
      deletedRoomIds.push(membership.roomId);
    } else {
      // Preserve access data when the room record is temporarily unavailable or
      // has an unknown status. Only an explicit Deleted state authorizes cleanup.
      memberships.push(membership);
    }
  }

  memberships.sort((left, right) =>
    String(right.joinedAtUtc).localeCompare(String(left.joinedAtUtc))
  );

  logger.info("Listed Player classrooms.", {
    playerId: context.playerId,
    classroomCount: memberships.length
  });

  return { memberships, deletedRoomIds };
};

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

module.exports.params = {};
