// Hàm phụ được gọi bởi nhiều hàm render như createAvatar(), openUserProfile(), sendFriendRequestFromCard().
// Dùng để trả về giá trị hợp lệ hoặc nội dung mặc định khi dữ liệu server trả về bị trống.
export function valueOr(value, fallback = "Chưa cập nhật") {
    return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

// Hàm phụ được gọi bởi createAvatar() và createMessageAvatar().
// Dùng để lấy chữ cái đầu tiên làm avatar khi user/conversation chưa có ảnh.
export function initialOf(value) {
    return valueOr(value, "?").charAt(0).toUpperCase(); //<=> value || fallback, charAt: lấy chữ đầu
}

// Hàm phụ được gọi bởi createAvamh tar(), renderCurrentUser(), messageSender(), openSettingsModal().
// Dùng để đọc avatarUrl với nhiều kiểu tên field khác nhau từ response cũ/mới.
export function avatarUrlOf(source) {
    return source?.avatarUrl || source?.AvatarUrl || source?.avatarURL || source?.AvatarURL || source?.photoUrl || source?.imageUrl || "";
}

// Hàm phụ được gọi bởi renderList(), loadChatData(), selectConversation(), searchUsers().
// Dùng để hiển thị trạng thái trống hoặc trạng thái đang tải trong một container.
export function setEmpty(container, text) {
    const empty = document.createElement("p");
    empty.className = "empty-state";
    empty.textContent = text;
    container.replaceChildren(empty);
}

// Hàm phụ được gọi bởi createConversationCard(), renderGroupInfo(), openUserProfile(), createFriendRequestCard().
// Dùng để tạo avatar chung có hỗ trợ ảnh thật hoặc chữ cái đầu, kèm animation tai thỏ bằng CSS.
export function createAvatar(source) {
    const name = typeof source === "string" ? source : conversationName(source);
    const avatarUrl = typeof source === "string" ? "" : avatarUrlOf(source);

    //tạo thẻ span
    const avatar = document.createElement("span");
    avatar.className = "avatar";  //<span class="avatar"></span>

    if (avatarUrl) {
        const image = document.createElement("img");
        image.src = avatarUrl; // gán link
        image.alt = name; //gán name alt
        avatar.appendChild(image); //đưa ảnh vào span
    } else {
        avatar.textContent = initialOf(name);
    }

    return avatar;
}

// Hàm phụ được gọi bởi renderCurrentUser() và selectConversation().
// Dùng để đổ avatar mới vào element đã có sẵn trong layout.
export function renderAvatarInto(element, source) {
    const avatar = createAvatar(source);
    element.replaceChildren(...avatar.childNodes);  //Lấy tất cả node con của avatar.
}

// Hàm phụ được gọi bởi userDisplayName() và openSettingsModal().
// Dùng để lấy field của user, hỗ trợ cả camelCase và PascalCase.
export function userField(user, camelName, pascalName = "") {
    return user?.[camelName] ?? user?.[pascalName || camelName.charAt(0).toUpperCase() + camelName.slice(1)] ?? "";
}

// Hàm phụ được gọi bởi renderCurrentUser() và messageSender().
// Dùng để tạo tên hiển thị của user từ displayName, họ tên hoặc username. kiểm tra lần lượt các điều kiện
export function userDisplayName(user) {
    return valueOr(
        user?.displayname || user?.displayName || user?.DisplayName ||
        `${userField(user, "firstName")} ${userField(user, "lastName")}`.trim() ||
        user?.userName || user?.username || user?.Username,
        "User"
    );
}

// Hàm phụ được gọi bởi renderCurrentUser().
// Dùng để lấy username của user hiện tại.
export function userName(user) {
    return valueOr(user?.userName || user?.username || user?.Username, "user");
}

// Hàm phụ được gọi bởi hầu hết các hàm xử lý data như selectConversation(), upsertConversation(), acceptFriendRequest().
// Dùng để chuẩn hóa id vì Mongo/.NET có thể trả id, _id hoặc _Id.
export function itemId(item) {
    return item?.id || item?._id || item?._Id || "";
}

// Hàm phụ được gọi bởi createAvatar(), createConversationCard(), renderGroupInfo(), renderFriendListSummary().
// Dùng để chuẩn hóa tên hiển thị của nhóm chat, bạn bè hoặc user.
export function conversationName(item) {
    return valueOr(item?.name || item?.displayname || item?.displayName || item?.userName || item?.username, "Cuộc trò chuyện");
}

// Hàm phụ được gọi bởi openConversationInfo() và selectConversation().
// Dùng để tìm userId của người đang chat riêng, phục vụ nút xem profile.
export function directChatUserId(conversation, friends = []) {
    if (!conversation || conversation.type !== "direct") return "";

    const directId = conversation.directUserId || conversation.userId || conversation.friendId;
    if (directId) return directId;

    const conversationUserName = conversation.userName || conversation.username;
    const friend = friends.find(item => {
        const friendConversationId = itemId(item.conversation);
        return friendConversationId === itemId(conversation)
            || (conversationUserName && (item.userName || item.username) === conversationUserName);
    });

    return itemId(friend);
}

// Hàm phụ được gọi bởi createConversationCard(), acceptFriendRequest() và các event SignalR kết bạn.
// Dùng để kiểm tra user đã là bạn bè hay chưa.
export function isFriend(friends = [], userId) {
    return friends.some(friend => itemId(friend) === userId);
}

// Hàm phụ được gọi bởi createConversationCard().
// Dùng để kiểm tra đã có lời mời kết bạn pending với user chưa.
export function hasPendingRequest(friendRequests, userId) {
    return [...(friendRequests?.received || []), ...(friendRequests?.sent || [])] // gopop 2 mảng thành 1
        .some(request => itemId(request.user) === userId);
} 

// Hàm phụ được gọi bởi createAddGroupMembersForm().
// Dùng để lấy danh sách id thành viên hiện có trong nhóm, tránh hiển thị người đã thuộc nhóm.
export function groupMemberIds(group) {
    return new Set((group?.members || []).map(member => itemId(member)).filter(Boolean));
}

// Hàm phụ được gọi bởi sortByLatestConversation().
// Dùng để lấy mốc thời gian ưu tiên khi sắp xếp conversation/card chat.
export function conversationSortTime(item) {
    const conversation = item?.conversation || item || {};
    return new Date(
        conversation.lastMessageAt ||
        conversation.updatedAt ||
        conversation.createdAt ||
        item?.lastMessageAt ||
        item?.updatedAt ||
        0
    ).getTime();
}

// Hàm phụ được gọi bởi renderGroups(), renderFriends(), updateConversationPreviewFromMessage(), upsertConversation().
// Dùng để đưa conversation có tin nhắn mới nhất lên đầu danh sách.
export function sortByLatestConversation(items = []) {
    return [...items].sort((a, b) => conversationSortTime(b) - conversationSortTime(a));
}

// Hàm phụ được gọi bởi renderMessages() và prependOlderMessages().
// Dùng để sắp xếp tin nhắn cũ ở trên, tin nhắn mới ở dưới.
export function sortMessagesByTime(messages = []) {
    return [...messages].sort((a, b) => new Date(a.createdAt || 0) - new Date(b.createdAt || 0));
}

// Hàm phụ được gọi bởi selectConversation() và loadOlderMessages().
// Dùng để đọc danh sách message từ response API, hỗ trợ cả response dạng array và response phân trang.
export function readMessagePage(data) {
    return {
        messages: Array.isArray(data) ? data : data.messages || [],
        nextCursor: Array.isArray(data) ? null : data.nextCursor || null
    };
}

// Hàm phụ được gọi bởi appendRealtimeMessage(), updateConversationPreviewFromMessage() và submit message form.
// Dùng để lấy conversationId từ message, hỗ trợ cả camelCase và PascalCase.
export function messageConversationId(message) {
    return message?.conversationId || message?.ConversationId || "";
}

// Hàm phụ được gọi bởi createMessage().
// Dùng để lấy tên/avatar người gửi từ currentUser, thành viên nhóm hoặc dữ liệu gắn trên message.
export function messageSender(message, currentUser, activeConversation) {
    const senderId = message?.senderId || message?.SenderId || "";
    if (senderId && senderId === itemId(currentUser)) {
        return {
            displayName: userDisplayName(currentUser),
            avatarUrl: avatarUrlOf(currentUser)
        };
    }

    const member = (activeConversation?.members || [])
        .find(item => itemId(item) === senderId);

    return {
        displayName: message?.senderName || message?.senderDisplayName || member?.displayname || member?.displayName || member?.userName || member?.username || "User",
        avatarUrl: message?.senderAvatarUrl || message?.SenderAvatarUrl || avatarUrlOf(member)
    };
}

// Hàm phụ được gọi bởi createMessage().
// Dùng để tạo avatar nhỏ đi kèm từng dòng tin nhắn, kể cả chat nhóm và chat riêng.
export function createMessageAvatar(sender) {
    const avatar = document.createElement("span");
    avatar.className = "message-avatar";

    if (sender.avatarUrl) {
        const image = document.createElement("img");
        image.src = sender.avatarUrl;
        image.alt = sender.displayName;
        avatar.appendChild(image);
    } else {
        avatar.textContent = initialOf(sender.displayName);
    }

    return avatar;
}
