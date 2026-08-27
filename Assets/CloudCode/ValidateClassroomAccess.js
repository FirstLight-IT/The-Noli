const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const MEMBERSHIPS_KEY = "classroom_memberships";
const ROOM_KEY = "room";

module.exports = async ({ params, context }) => {
  const roomId = clean(params.roomId);
  const cloudSave = new DataApi(context);
  const profile = await loadProtectedValue(
    cloudSave, context.projectId, context.playerId, PROFILE_KEY
  );
  if (!profile || profile.role === "Librarian") {
    return denied("Player access is required.", "Denied");
  }

  const room = await loadPrivateCustomValue(
    cloudSave, context.projectId, roomEntityId(roomId), ROOM_KEY
  );
  if (!room) {
    // Cloud Save custom-item reads can temporarily fail to return a legacy room.
    // Absence is not proof of deletion, so preserve cached classroom access. Only
    // an explicit Deleted/Archived state is allowed to revoke and erase locally.
    return { success: true, error: "", status: "Unknown" };
  }
  if (room.status === "Deleted" || room.status === "Archived") {
    return denied("This classroom has been deleted by its Teacher.", "Deleted");
  }
  if (room.status !== "Active") {
    return denied("This classroom is not currently available.", room.status);
  }

  const savedMemberships = await loadProtectedValue(
    cloudSave, context.projectId, context.playerId, MEMBERSHIPS_KEY
  );
  const memberships = Array.isArray(savedMemberships) ? savedMemberships.slice() : [];
  let membership = memberships.find(item => item && item.roomId === roomId);
  room.members = Array.isArray(room.members) ? room.members : [];
  let roomMember = room.members.find(item => item && item.accountId === context.playerId);
  const protectedActive = membership && membership.status === "Active";
  const roomRosterActive = roomMember && roomMember.status === "Active";

  if (!protectedActive && !roomRosterActive) {
    return denied("You are no longer an active member of this classroom.", "Denied");
  }

  // A successful Join writes both records and Leave marks both inactive. If only
  // one active record remains, repair the stale counterpart instead of blocking
  // a legitimate member.
  if (!protectedActive) {
    membership = membership || {
      roomId,
      joinedAtUtc: roomMember && roomMember.joinedAtUtc
        ? roomMember.joinedAtUtc
        : new Date().toISOString()
    };
    membership.roomName = room.roomName;
    membership.teacherAccountId = room.teacherAccountId;
    membership.teacherInGameName = room.teacherInGameName;
    membership.status = "Active";
    membership.leftAtUtc = "";
    if (!memberships.includes(membership)) memberships.push(membership);
    await cloudSave.setProtectedItem(context.projectId, context.playerId, {
      key: MEMBERSHIPS_KEY,
      value: memberships
    });
  }

  if (!roomRosterActive) {
    roomMember = roomMember || {
      accountId: context.playerId,
      joinedAtUtc: membership.joinedAtUtc || new Date().toISOString()
    };
    roomMember.inGameName = clean(profile.inGameName);
    roomMember.status = "Active";
    roomMember.leftAtUtc = "";
    if (!room.members.includes(roomMember)) room.members.push(roomMember);
    await cloudSave.setPrivateCustomItem(
      context.projectId,
      roomEntityId(roomId),
      { key: ROOM_KEY, value: room }
    );
  }

  return { success: true, error: "", status: room.status };
};

function denied(error, status) {
  return { success: false, error, status };
}
function clean(value) { return typeof value === "string" ? value.trim() : ""; }
function roomEntityId(roomId) { return `classroom_${roomId}`; }

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

module.exports.params = {
  roomId: { type: "String", required: true }
};
