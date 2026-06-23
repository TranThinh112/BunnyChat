import { apiFetch } from "./Api_Fetch.js";

const el = id => document.getElementById(id);

const sidebar = el("chatSidebar");
const groupList = el("groupList");
const friendList = el("friendList");
const messageArea = el("messageArea");
const messageForm = el("messageForm");
const messageInput = el("messageInput");
const sendButton = messageForm.querySelector(".send-button");
const searchBox = el("searchBox");
const searchResult = el("searchResult");
const keywordInput = el("keyword");
const searchButton = el("searchBtn");
const themeToggle = document.querySelector(".theme-toggle");
const profileModal = el("profileModal");
const profileModalContent = el("profileModalContent");
const groupInfo = el("groupInfo");
const groupInfoContent = el("groupInfoContent");
const groupInfoButton = el("groupInfoButton");
const friendModal = el("friendModal");
const friendRequestList = el("friendRequestList");
const friendRequestBadge = el("friendRequestBadge");
const friendListSummary = el("friendListSummary");
const friendRequestMessageModal = el("friendRequestMessageModal");
const friendRequestMessageForm = el("friendRequestMessageForm");
const friendRequestMessageInput = el("friendRequestMessageInput");
const friendMessageSubtitle = el("friendMessageSubtitle");
const groupCreateModal = el("groupCreateModal");
const groupCreateForm = el("groupCreateForm");
const groupNameInput = el("groupNameInput");
const groupMemberPicker = el("groupMemberPicker");
const groupNameError = el("groupNameError");
const groupMembersError = el("groupMembersError");
const settingsModal = el("settingsModal");
const settingsForm = el("settingsForm");
const settingsFirstName = el("settingsFirstName");
const settingsLastName = el("settingsLastName");
const settingsNickname = el("settingsNickname");
const settingsPhone = el("settingsPhone");
const settingsAvatarUrl = el("settingsAvatarUrl");
const settingsBio = el("settingsBio");
const randomLogoColorBtn = el("randomLogoColorBtn");

const API = {
    conversations: "/api/chat",
    friends: "/api/friends",
    friendRequests: "/api/friends/requests",
    sendFriendRequest: "/api/friends/requests",
    acceptFriendRequest: id => `/api/friends/requests/${encodeURIComponent(id)}/accept`,
    declineFriendRequest: id => `/api/friends/requests/${encodeURIComponent(id)}/decline`,
    createConversation: "/api/chat",
    addGroupMembers: id => `/api/chat/${encodeURIComponent(id)}/members`,
    messages: (id, cursor = "") => {
        const url = `/api/chat/${encodeURIComponent(id)}/messages`;
        return cursor ? `${url}?cursor=${encodeURIComponent(cursor)}` : url;
    },
    sendMessage: id => `/api/chat/${encodeURIComponent(id)}/messages`,
    seen: id => `/api/chat/${encodeURIComponent(id)}/seen`,
    search: keyword => `/api/users/search?q=${encodeURIComponent(keyword)}`,
    profile: userId => `/api/users/${encodeURIComponent(userId)}/profile`,
    me: "/api/users/me",
    updateMe: "/api/users/me",
    ...(window.BunnyChatAPI || {})
};

const state = {
    currentUser: null,
    conversations: [],
    friends: [],
    friendRequests: {
        received: [],
        sent: []
    },
    friendRequestTab: "received",
    activeConversation: null,
    pendingFriendRequest: null,
    connection: null,
    joinedConversationIds: new Set(),
    renderedMessageIds: new Set(),
    messageNextCursor: null,
    loadingOlderMessages: false
};

// Trả về giá trị hợp lệ hoặc nội dung mặc định khi dữ liệu bị trống.
function valueOr(value, fallback = "Chưa cập nhật") {
    return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

// Lấy chữ cái đầu để hiển thị avatar.
function initialOf(value) {
    return valueOr(value, "?").charAt(0).toUpperCase();
}

// Lấy avatarUrl với nhiều kiểu tên field để tương thích response cũ/mới.
function avatarUrlOf(source) {
    return source?.avatarUrl || source?.AvatarUrl || source?.avatarURL || source?.AvatarURL || source?.photoUrl || source?.imageUrl || "";
}

// Hiển thị trạng thái trống hoặc trạng thái đang tải trong container.
function setEmpty(container, text) {
    const empty = document.createElement("p");
    empty.className = "empty-state";
    empty.textContent = text;
    container.replaceChildren(empty);
}

// Tạo phần tử avatar có animation tai thỏ theo CSS hiện tại.
function createAvatar(source) {
    const name = typeof source === "string" ? source : conversationName(source);
    const avatarUrl = typeof source === "string" ? "" : avatarUrlOf(source);
    const avatar = document.createElement("span");
    avatar.className = "avatar";

    if (avatarUrl) {
        const image = document.createElement("img");
        image.src = avatarUrl;
        image.alt = name;
        avatar.appendChild(image);
    } else {
        avatar.textContent = initialOf(name);
    }

    return avatar;
}

// Đổ nội dung avatar vào element đã có sẵn trong layout.
function renderAvatarInto(element, source) {
    const avatar = createAvatar(source);
    element.replaceChildren(...avatar.childNodes);
}

function userField(user, camelName, pascalName = "") {
    return user?.[camelName] ?? user?.[pascalName || camelName.charAt(0).toUpperCase() + camelName.slice(1)] ?? "";
}

function userDisplayName(user) {
    return valueOr(
        user?.displayname || user?.displayName || user?.DisplayName ||
        `${userField(user, "firstName")} ${userField(user, "lastName")}`.trim() ||
        user?.userName || user?.username || user?.Username,
        "User"
    );
}

function userName(user) {
    return valueOr(user?.userName || user?.username || user?.Username, "user");
}

function renderCurrentUser() {
    const user = state.currentUser;
    el("currentDisplayname").textContent = userDisplayName(user);
    el("currentUsername").textContent = `@${userName(user)}`;
    renderAvatarInto(el("currentAvatar"), user);
}

// Chuẩn hóa id vì Mongo/.NET có thể trả id hoặc _id.
function itemId(item) {
    return item?.id || item?._id || item?._Id || "";
}

// Chuẩn hóa tên hiển thị của nhóm chat, bạn bè hoặc người dùng.
function conversationName(item) {
    return valueOr(item?.name || item?.displayname || item?.displayName || item?.userName || item?.username, "Cuộc trò chuyện");
}

// Lấy userId của người đang chat riêng để mở profile từ nút thông tin.
function directChatUserId(conversation) {
    if (!conversation || conversation.type !== "direct") return "";

    const directId = conversation.directUserId || conversation.userId || conversation.friendId;
    if (directId) return directId;

    const conversationUserName = conversation.userName || conversation.username;
    const friend = state.friends.find(item => {
        const friendConversationId = itemId(item.conversation);
        return friendConversationId === itemId(conversation)
            || (conversationUserName && (item.userName || item.username) === conversationUserName);
    });

    return itemId(friend);
}

// Kiểm tra user đã là bạn bè chưa để quyết định hiển thị nút kết bạn.
function isFriend(userId) {
    return state.friends.some(friend => itemId(friend) === userId);
}

// Kiểm tra đã có lời mời kết bạn pending với user chưa.
function hasPendingRequest(userId) {
    return [...state.friendRequests.received, ...state.friendRequests.sent]
        .some(request => itemId(request.user) === userId);
}

// Lấy danh sách id thành viên của nhóm để tránh thêm trùng ở UI.
function groupMemberIds(group) {
    return new Set((group?.members || []).map(member => itemId(member)).filter(Boolean));
}

// Lấy dữ liệu chính từ ApiResponse của ASP.NET.
async function fetchData(url, options) {
    const response = await apiFetch(url, options);
    const result = await response.json().catch(() => ({}));

    if (!response.ok) {
        const validationMessage = result.errors
            ? Object.values(result.errors).flat().join("\n")
            : "";
        throw new Error(result.message || validationMessage || result.title || "Request failed");
    }

    return result.data ?? result;
}

// Gửi lời mời kết bạn từ card user trong kết quả tìm kiếm.
async function sendFriendRequestFromCard(user, button, messageText = "") {
    const oldText = button.textContent;
    button.disabled = true;
    button.textContent = "Đang gửi...";

    try {
        const request = await fetchData(API.sendFriendRequest, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                ReceiveId: itemId(user),
                Message: valueOr(messageText, "Mình muốn kết bạn với bạn.")
            })
        });

        state.friendRequests.sent.unshift({
            id: itemId(request),
            message: request.message,
            createdAt: request.createdAt,
            user
        });

        renderFriendRequestBadge();
        renderFriendRequests();
        button.textContent = "Đã gửi";
    } catch (error) {
        button.disabled = false;
        button.textContent = oldText;
        alert(error.message || "Không thể gửi lời mời kết bạn.");
    }
}

function openFriendRequestMessageModal(user, button) {
    state.pendingFriendRequest = { user, button };
    friendMessageSubtitle.textContent = `Gửi lời mời đến ${conversationName(user)}`;
    friendRequestMessageInput.value = "Mình muốn kết bạn với bạn.";
    friendRequestMessageModal.classList.add("open");
    friendRequestMessageModal.setAttribute("aria-hidden", "false");
    friendRequestMessageInput.focus();
    friendRequestMessageInput.select();
}

function closeFriendRequestMessageModal() {
    friendRequestMessageModal.classList.remove("open");
    friendRequestMessageModal.setAttribute("aria-hidden", "true");
    state.pendingFriendRequest = null;
}

async function submitFriendRequestMessage(event) {
    event.preventDefault();
    const pending = state.pendingFriendRequest;
    if (!pending) return;

    const messageText = friendRequestMessageInput.value.trim();
    closeFriendRequestMessageModal();
    await sendFriendRequestFromCard(pending.user, pending.button, messageText);
}

// Gọi API tạo direct conversation nếu bạn bè chưa có conversation.
async function getOrCreateDirectConversation(friend) {
    if (friend.conversation) {
        return {
            ...friend.conversation,
            directUserId: friend.conversation.directUserId || itemId(friend),
            userName: friend.conversation.userName || friend.userName || friend.username,
            username: friend.conversation.username || friend.username,
            displayname: friend.conversation.displayname || friend.displayname || friend.displayName
        };
    }

    const conversation = await fetchData(API.createConversation, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            type: "direct",
            memberIds: [itemId(friend)]
        })
    });

    const directConversation = {
        ...conversation,
        directUserId: conversation.directUserId || itemId(friend),
        userName: conversation.userName || friend.userName || friend.username,
        username: conversation.username || friend.username,
        displayname: conversation.displayname || friend.displayname || friend.displayName
    };

    upsertConversation(directConversation);
    return directConversation;
}

// Tạo card cuộc trò chuyện và gắn hành động theo từng loại card.
function createConversationCard(item, type) {
    const name = conversationName(item);
    const card = document.createElement("button");
    card.type = "button";
    card.className = "conversation-card";
    card.dataset.id = itemId(item.conversation || item);
    card.dataset.type = type;
    if (card.dataset.id && card.dataset.id === itemId(state.activeConversation)) {
        card.classList.add("active");
    }

    const info = document.createElement("span");
    info.className = "conversation-info";

    const title = document.createElement("strong");
    title.textContent = name;

    const preview = document.createElement("small");
    preview.textContent = item.lastMessage
        || item.conversation?.lastMessage
        || item.userName
        || item.username
        || (type === "group" ? `${item.memberCount || 0} thành viên` : "Chưa có tin nhắn");

    info.append(title, preview);
    card.append(createAvatar(item), info);

    if (type === "search") {
        card.classList.add("search-user-card");
        card.addEventListener("click", () => openUserProfile(itemId(item)));

        const action = document.createElement("span");
        action.className = "search-card-actions";

        const addButton = document.createElement("button");
        addButton.type = "button";
        addButton.className = "mini-action";

        const userId = itemId(item);
        const isMe = userId && userId === itemId(state.currentUser);

        if (isMe) {
            addButton.textContent = "Bạn";
            addButton.disabled = true;
        } else if (isFriend(userId)) {
            addButton.textContent = "Đã là bạn";
            addButton.disabled = true;
        } else if (hasPendingRequest(userId)) {
            addButton.textContent = "Đã gửi/nhận";
            addButton.disabled = true;
        } else {
            addButton.textContent = "Kết bạn";
            addButton.addEventListener("click", event => {
                event.stopPropagation();
                openFriendRequestMessageModal(item, addButton);
            });
        }

        action.appendChild(addButton);
        card.appendChild(action);
    } else if (type === "friend") {
        card.addEventListener("click", async () => {
            setActiveCard(card);
            setEmpty(messageArea, "Đang mở cuộc trò chuyện...");
            const conversation = await getOrCreateDirectConversation(item);
            await selectConversation(conversation, card);
        });
    } else if (type !== "member") {
        card.addEventListener("click", () => selectConversation(item, card));
    }

    return card;
}

// Đánh dấu card đang được chọn.
function setActiveCard(card) {
    document.querySelector(".conversation-card.active")?.classList.remove("active");
    card?.classList.add("active");
}

// Render danh sách card vào container và cập nhật tổng số phần tử.
function renderList(container, count, items, type) {
    container.replaceChildren();
    count.textContent = String(items.length);

    if (!items.length) {
        setEmpty(container, type === "group" ? "Chưa có nhóm chat." : "Chưa có bạn bè.");
        return;
    }

    items.forEach(item => container.appendChild(createConversationCard(item, type)));
}

// Lấy mốc thời gian dùng để sắp xếp card chat mới nhất lên đầu.
function conversationSortTime(item) {
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

// Sắp xếp danh sách conversation/bạn bè theo tin nhắn mới nhất.
function sortByLatestConversation(items = []) {
    return [...items].sort((a, b) => conversationSortTime(b) - conversationSortTime(a));
}

// Render danh sách nhóm chat từ dữ liệu server.
function renderGroups() {
    const groups = sortByLatestConversation(state.conversations.filter(item => item.type === "group"));
    renderList(groupList, el("groupCount"), groups, "group");
}

// Render danh sách bạn bè và ghép direct conversation nếu đã tồn tại.
function renderFriends() {
    const directConversations = state.conversations.filter(item => item.type === "direct");
    const friends = state.friends.map(friend => {
        const id = itemId(friend);
        const friendUserName = friend.userName || friend.username;
        const conversation = directConversations.find(item =>
            item.directUserId === id || (item.userName || item.username) === friendUserName
        );

        return {
            ...friend,
            conversation,
            lastMessage: conversation?.lastMessage
        };
    });
    const sortedFriends = sortByLatestConversation(friends);

    renderList(friendList, el("friendCount"), sortedFriends, "friend");
    renderFriendListSummary(sortedFriends);
}

// Render card tóm tắt danh sách bạn bè để sidebar nhìn rõ hơn.
function renderFriendListSummary(friends) {
    if (!friendListSummary) return;

    if (!friends.length) {
        friendListSummary.textContent = "Chưa có bạn bè. Hãy tìm user và gửi lời mời kết bạn.";
        return;
    }

    const previewNames = friends
        .slice(0, 3)
        .map(friend => conversationName(friend))
        .join(", ");

    friendListSummary.textContent = `${friends.length} bạn bè: ${previewNames}${friends.length > 3 ? "..." : ""}`;
}

// Tạo phần tử tin nhắn gửi hoặc nhận từ object tin nhắn.
function renderGroupMemberPicker() {
    groupMemberPicker.replaceChildren();

    if (!state.friends.length) {
        setEmpty(groupMemberPicker, "Cần có bạn bè trước khi tạo nhóm.");
        return;
    }

    state.friends.forEach(friend => {
        const id = itemId(friend);
        const label = document.createElement("label");
        label.className = "member-option";

        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.value = id;

        const body = document.createElement("span");
        const info = document.createElement("span");
        info.className = "member-option-info";

        const name = document.createElement("strong");
        name.textContent = conversationName(friend);

        const username = document.createElement("small");
        username.textContent = `@${friend.userName || friend.username || "unknown"}`;

        info.append(name, username);
        body.append(createAvatar(friend), info);
        label.append(checkbox, body);
        groupMemberPicker.appendChild(label);
    });
}

function openGroupCreateModal() {
    groupNameInput.value = "";
    clearGroupCreateErrors();
    renderGroupMemberPicker();
    groupCreateModal.classList.add("open");
    groupCreateModal.setAttribute("aria-hidden", "false");
    groupNameInput.focus();
}

function closeGroupCreateModal() {
    groupCreateModal.classList.remove("open");
    groupCreateModal.setAttribute("aria-hidden", "true");
}

// Xóa thông báo lỗi trong modal tạo nhóm.
function clearGroupCreateErrors() {
    groupNameError.textContent = "";
    groupMembersError.textContent = "";
    groupNameInput.classList.remove("input-error");
}

// Hiện lỗi inline dưới field tạo nhóm, không dùng alert trình duyệt.
function showGroupCreateError(field, message) {
    if (field === "name") {
        groupNameError.textContent = message;
        groupNameInput.classList.add("input-error");
        groupNameInput.focus();
        return;
    }

    groupMembersError.textContent = message;
}

async function submitGroupCreate(event) {
    event.preventDefault();
    clearGroupCreateErrors();

    const name = groupNameInput.value.trim();
    const memberIds = Array
        .from(groupMemberPicker.querySelectorAll("input[type='checkbox']:checked"))
        .map(input => input.value)
        .filter(Boolean);

    if (!name) {
        showGroupCreateError("name", "Vui lòng nhập tên nhóm.");
        return;
    }

    if (!memberIds.length) {
        showGroupCreateError("members", "Vui lòng chọn ít nhất 1 thành viên.");
        return;
    }

    const submitButton = groupCreateForm.querySelector("button[type='submit']");
    submitButton.disabled = true;
    submitButton.textContent = "Đang tạo...";

    try {
        const conversation = await fetchData(API.createConversation, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                type: "group",
                name,
                memberIds
            })
        });

        closeGroupCreateModal();
        upsertConversation(conversation);
        renderGroups();
        renderFriends();
        await selectConversation(conversation);
    } catch (error) {
        showGroupCreateError("name", error.message || "Không thể tạo nhóm chat.");
    } finally {
        submitButton.disabled = false;
        submitButton.textContent = "Tạo nhóm";
    }
}

function createMessage(message) {
    const currentUserId = itemId(state.currentUser);
    const senderId = message.senderId || message.SenderId || "";
    const isMine = senderId
        ? senderId === currentUserId
        : Boolean(message.isMine || message.isOwn);
    const sender = messageSender(message);
    const row = document.createElement("article");
    row.className = `message ${isMine ? "outgoing" : "incoming"}`;
    row.title = sender.displayName;

    const body = document.createElement("span");
    body.className = "message-body";

    const senderName = document.createElement("strong");
    senderName.className = "message-sender";
    senderName.textContent = sender.displayName;

    const bubble = document.createElement("p");
    bubble.textContent = message.content || message.text || "";

    const time = document.createElement("time");
    time.textContent = message.createdAt
        ? new Date(message.createdAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })
        : "";

    body.append(senderName, bubble, time);
    row.append(createMessageAvatar(sender), body);
    return row;
}

// Sắp xếp tin nhắn cũ ở trên, tin nhắn mới ở dưới để khung chat đọc đúng thứ tự.
function sortMessagesByTime(messages = []) {
    return [...messages].sort((a, b) => new Date(a.createdAt || 0) - new Date(b.createdAt || 0));
}

// Lay danh sach message tu response cua API, ho tro ca response moi va response cu.
function readMessagePage(data) {
    return {
        messages: Array.isArray(data) ? data : data.messages || [],
        nextCursor: Array.isArray(data) ? null : data.nextCursor || null
    };
}

// Kiểm tra người dùng có đang ở gần cuối khung chat không để quyết định auto scroll.
function isNearMessageBottom() {
    return messageArea.scrollHeight - messageArea.scrollTop - messageArea.clientHeight < 140;
}

// Lấy conversationId của message, hỗ trợ cả camelCase và PascalCase.
function messageConversationId(message) {
    return message?.conversationId || message?.ConversationId || "";
}

// Lấy thông tin người gửi để hiển thị avatar cạnh tin nhắn.
function messageSender(message) {
    const senderId = message?.senderId || message?.SenderId || "";
    if (senderId && senderId === itemId(state.currentUser)) {
        return {
            displayName: userDisplayName(state.currentUser),
            avatarUrl: avatarUrlOf(state.currentUser)
        };
    }

    const member = (state.activeConversation?.members || [])
        .find(item => itemId(item) === senderId);

    return {
        displayName: message?.senderName || message?.senderDisplayName || member?.displayname || member?.displayName || member?.userName || member?.username || "User",
        avatarUrl: message?.senderAvatarUrl || message?.SenderAvatarUrl || avatarUrlOf(member)
    };
}

// Tạo avatar nhỏ cho từng dòng tin nhắn.
function createMessageAvatar(sender) {
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

// Render toàn bộ tin nhắn của cuộc trò chuyện đang được chọn.
function renderMessages(messages = []) {
    messageArea.replaceChildren();
    state.renderedMessageIds.clear();

    const sortedMessages = sortMessagesByTime(messages);
    sortedMessages.forEach(message => {
        const id = itemId(message);
        if (id) state.renderedMessageIds.add(id);
        messageArea.appendChild(createMessage(message));
    });
    if (!sortedMessages.length) setEmpty(messageArea, "Chưa có tin nhắn.");

    messageArea.scrollTop = messageArea.scrollHeight;
}

// Chèn tin nhắn cũ vào đầu danh sách và giữ vị trí đang đọc hiện tại.
function prependOlderMessages(messages = []) {
    const sortedMessages = sortMessagesByTime(messages);
    if (!sortedMessages.length) return;

    const previousHeight = messageArea.scrollHeight;
    const fragment = document.createDocumentFragment();

    sortedMessages.forEach(message => {
        const id = itemId(message);
        if (id && state.renderedMessageIds.has(id)) return;
        if (id) state.renderedMessageIds.add(id);
        fragment.appendChild(createMessage(message));
    });

    if (!fragment.childNodes.length) return;

    messageArea.prepend(fragment);
    messageArea.scrollTop = messageArea.scrollHeight - previousHeight;
}

// Tải trang tin nhắn cũ hơn khi người dùng kéo lên đầu khung chat.
async function loadOlderMessages() {
    const conversationId = itemId(state.activeConversation);
    if (!conversationId || !state.messageNextCursor || state.loadingOlderMessages) return;
    if (messageArea.querySelector(".empty-state")) return;

    state.loadingOlderMessages = true;
    try {
        const data = await fetchData(API.messages(conversationId, state.messageNextCursor));
        const page = readMessagePage(data);
        state.messageNextCursor = page.nextCursor;
        prependOlderMessages(page.messages);
    } catch {
        // Không chặn việc đọc chat nếu trang tin nhắn cũ tải thất bại.
    } finally {
        state.loadingOlderMessages = false;
    }
}

// Thêm một tin nhắn realtime vào khung chat nếu đang mở đúng conversation.
function appendRealtimeMessage(message) {
    const activeId = itemId(state.activeConversation);
    if (!activeId || messageConversationId(message) !== activeId) return;

    const id = itemId(message);
    if (id && state.renderedMessageIds.has(id)) return;
    if (id) state.renderedMessageIds.add(id);

    const shouldScrollToBottom = isNearMessageBottom() || message.senderId === itemId(state.currentUser);
    messageArea.querySelector(".empty-state")?.remove();
    messageArea.appendChild(createMessage(message));
    if (shouldScrollToBottom) {
        messageArea.scrollTop = messageArea.scrollHeight;
    }

    markActiveConversationAsSeen();
}

// Cập nhật hoặc thêm conversation vào state.
// Cập nhật preview conversation từ tin nhắn mới để card chat được đẩy lên đầu.
function updateConversationPreviewFromMessage(message) {
    const conversationId = messageConversationId(message);
    if (!conversationId) return;

    const index = state.conversations.findIndex(item => itemId(item) === conversationId);
    if (index < 0) return;

    const messageTime = message.createdAt || new Date().toISOString();
    state.conversations[index] = {
        ...state.conversations[index],
        lastMessage: message.content || message.text || state.conversations[index].lastMessage,
        lastMessageAt: messageTime,
        updatedAt: messageTime
    };

    state.conversations = sortByLatestConversation(state.conversations);
    renderGroups();
    renderFriends();
}

function upsertConversation(conversation) {
    if (!conversation) return;

    const id = itemId(conversation);
    const index = state.conversations.findIndex(item => itemId(item) === id);

    if (index >= 0) {
        state.conversations[index] = { ...state.conversations[index], ...conversation };
    } else {
        state.conversations.unshift(conversation);
    }

    state.conversations = sortByLatestConversation(state.conversations);
    if (itemId(state.activeConversation) === id) {
        state.activeConversation = { ...state.activeConversation, ...conversation };
    }
    renderGroups();
    renderFriends();
    joinConversation(id);
}

// Chọn cuộc trò chuyện, cập nhật header và tải danh sách tin nhắn.
async function selectConversation(item, card) {
    state.activeConversation = item;
    setActiveCard(card);

    const name = conversationName(item);
    el("chatTitle").textContent = name;
    el("chatStatus").textContent = item.type === "group"
        ? `${item.memberCount || item.members?.length || 0} thành viên`
        : "Tin nhắn riêng";
    renderAvatarInto(el("chatAvatar"), item);

    groupInfoButton.disabled = item.type !== "group" && !directChatUserId(item);
    groupInfoButton.setAttribute("aria-label", item.type === "group" ? "Thông tin nhóm" : "Xem profile");
    groupInfoButton.title = item.type === "group" ? "Thông tin nhóm" : "Xem profile";
    renderGroupInfo(item.type === "group" ? item : null);

    messageInput.disabled = false;
    sendButton.disabled = false;
    messageInput.placeholder = "Soạn tin nhắn...";
    state.messageNextCursor = null;
    state.loadingOlderMessages = false;

    setEmpty(messageArea, "Đang tải tin nhắn...");
    joinConversation(itemId(item));

    try {
        const data = await fetchData(API.messages(itemId(item)));
        const page = readMessagePage(data);
        state.messageNextCursor = page.nextCursor;
        renderMessages(page.messages);
        markActiveConversationAsSeen();
    } catch {
        setEmpty(messageArea, "Chưa thể tải tin nhắn từ máy chủ.");
    }
}

// Render thông tin nhóm gồm tên, ngày tạo và danh sách thành viên.
function renderGroupInfo(group) {
    groupInfoContent.replaceChildren();
    if (!group) return;

    const name = conversationName(group);
    const hero = document.createElement("div");
    hero.className = "group-info-hero";
    hero.append(createAvatar(name));

    const heroText = document.createElement("div");
    const title = document.createElement("h2");
    title.textContent = name;
    const count = document.createElement("p");
    count.textContent = `${group.memberCount || group.members?.length || 0} thành viên`;
    heroText.append(title, count);
    hero.append(heroText);

    const details = document.createElement("dl");
    const createdAt = group.createdAt
        ? new Date(group.createdAt).toLocaleDateString("vi-VN")
        : "Chưa cập nhật";

    [["Tên nhóm", name], ["Số thành viên", String(group.memberCount || group.members?.length || 0)], ["Ngày tạo", createdAt]]
        .forEach(([label, value]) => {
            const row = document.createElement("div");
            const dt = document.createElement("dt");
            const dd = document.createElement("dd");
            dt.textContent = label;
            dd.textContent = value;
            row.append(dt, dd);
            details.append(row);
        });

    const members = document.createElement("div");
    members.className = "group-members";
    const membersTitle = document.createElement("strong");
    membersTitle.textContent = "Thành viên";
    members.append(membersTitle);
    (group.members || []).forEach(member => members.append(createConversationCard(member, "member")));

    groupInfoContent.append(hero, details, members, createAddGroupMembersForm(group));
}

// Tạo form thêm bạn bè chưa thuộc nhóm vào group chat hiện tại.
function createAddGroupMembersForm(group) {
    const wrapper = document.createElement("form");
    wrapper.className = "group-add-members";

    const title = document.createElement("strong");
    title.textContent = "Thêm thành viên";

    const memberIds = groupMemberIds(group);
    const candidates = state.friends.filter(friend => !memberIds.has(itemId(friend)));

    const picker = document.createElement("div");
    picker.className = "member-picker compact-picker";

    const error = document.createElement("small");
    error.className = "field-error";

    if (!candidates.length) {
        const empty = document.createElement("p");
        empty.className = "empty-state compact-empty";
        empty.textContent = "Không còn bạn bè nào để thêm vào nhóm.";
        picker.appendChild(empty);
    } else {
        candidates.forEach(friend => {
            const label = document.createElement("label");
            label.className = "member-option";

            const checkbox = document.createElement("input");
            checkbox.type = "checkbox";
            checkbox.value = itemId(friend);

            const body = document.createElement("span");
            const info = document.createElement("span");
            info.className = "member-option-info";

            const name = document.createElement("strong");
            name.textContent = conversationName(friend);

            const username = document.createElement("small");
            username.textContent = `@${friend.userName || friend.username || "unknown"}`;

            info.append(name, username);
            body.append(createAvatar(friend), info);
            label.append(checkbox, body);
            picker.appendChild(label);
        });
    }

    const actions = document.createElement("div");
    actions.className = "modal-actions";

    const submit = document.createElement("button");
    submit.className = "friend-action";
    submit.type = "submit";
    submit.textContent = "Thêm thành viên";
    submit.disabled = !candidates.length;
    actions.appendChild(submit);

    wrapper.append(title, picker, error, actions);
    wrapper.addEventListener("submit", event => submitAddGroupMembers(event, group, error, submit));

    return wrapper;
}

// Gửi danh sách thành viên mới lên backend; backend sẽ bỏ/chặn user đã thuộc nhóm.
async function submitAddGroupMembers(event, group, error, submitButton) {
    event.preventDefault();
    error.textContent = "";

    const memberIds = Array
        .from(event.currentTarget.querySelectorAll("input[type='checkbox']:checked"))
        .map(input => input.value)
        .filter(Boolean);

    if (!memberIds.length) {
        error.textContent = "Vui lòng chọn ít nhất 1 thành viên.";
        return;
    }

    submitButton.disabled = true;
    submitButton.textContent = "Đang thêm...";

    try {
        const updatedConversation = await fetchData(API.addGroupMembers(itemId(group)), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ memberIds })
        });

        upsertConversation(updatedConversation);
        renderGroupInfo(updatedConversation);

        el("chatStatus").textContent = `${updatedConversation.memberCount || updatedConversation.members?.length || 0} thành viên`;
    } catch (apiError) {
        error.textContent = apiError.message || "Không thể thêm thành viên vào nhóm.";
    } finally {
        submitButton.disabled = false;
        submitButton.textContent = "Thêm thành viên";
    }
}

// Mở drawer thông tin của nhóm đang được chọn.
function openGroupInfo() {
    if (!state.activeConversation || state.activeConversation.type !== "group") return;
    groupInfo.classList.add("open");
    groupInfo.setAttribute("aria-hidden", "false");
}

// Mở thông tin của conversation hiện tại: group thì mở drawer, direct thì mở profile user.
function openConversationInfo() {
    if (!state.activeConversation) return;

    if (state.activeConversation.type === "group") {
        openGroupInfo();
        return;
    }

    openUserProfile(directChatUserId(state.activeConversation));
}

// Đóng drawer thông tin nhóm.
function closeGroupInfo() {
    groupInfo.classList.remove("open");
    groupInfo.setAttribute("aria-hidden", "true");
}

// Cập nhật số thông báo lời mời kết bạn trên chuông.
function renderFriendRequestBadge() {
    const count = state.friendRequests.received.length;

    if (!friendRequestBadge) return;

    friendRequestBadge.hidden = count === 0;
    friendRequestBadge.textContent = String(count);
}

// Tải lời mời kết bạn đã nhận và đã gửi từ backend.
async function loadFriendRequests() {
    try {
        const data = await fetchData(API.friendRequests);
        state.friendRequests.received = Array.isArray(data.received) ? data.received : [];
        state.friendRequests.sent = Array.isArray(data.sent) ? data.sent : [];
    } catch {
        state.friendRequests.received = [];
        state.friendRequests.sent = [];
    }

    renderFriendRequestBadge();
    renderFriendRequests();
}

// Mở modal thông báo lời mời kết bạn.
function openFriendModal() {
    friendModal.classList.add("open");
    friendModal.setAttribute("aria-hidden", "false");
    renderFriendRequests();
}

// Đóng modal thông báo lời mời kết bạn.
function closeFriendModal() {
    friendModal.classList.remove("open");
    friendModal.setAttribute("aria-hidden", "true");
}

// Đổi tab lời mời đã nhận / đã gửi.
function setFriendRequestTab(tab) {
    state.friendRequestTab = tab;

    document.querySelectorAll("[data-friend-tab]").forEach(button => {
        button.classList.toggle("active", button.dataset.friendTab === tab);
    });

    renderFriendRequests();
}

// Render danh sách lời mời kết bạn trong modal thông báo.
function renderFriendRequests() {
    if (!friendRequestList) return;

    const list = state.friendRequests[state.friendRequestTab] || [];
    friendRequestList.replaceChildren();

    if (!list.length) {
        setEmpty(
            friendRequestList,
            state.friendRequestTab === "received"
                ? "Chưa có lời mời kết bạn mới."
                : "Bạn chưa gửi lời mời nào."
        );
        return;
    }

    list.forEach(request => friendRequestList.appendChild(createFriendRequestCard(request, state.friendRequestTab)));
}

// Tạo card lời mời kết bạn trong modal chuông.
function createFriendRequestCard(request, tab) {
    const user = request.user || {};
    const card = document.createElement("article");
    card.className = "friend-request-card";

    const main = document.createElement("div");
    main.className = "friend-request-main";
    main.appendChild(createAvatar(user));

    const text = document.createElement("span");
    text.className = "friend-request-text";
    const name = document.createElement("strong");
    name.textContent = conversationName(user);
    const username = document.createElement("small");
    username.textContent = `@${user.userName || user.username || "unknown"}`;
    text.append(name, username);
    main.appendChild(text);
    card.appendChild(main);

    if (request.message) {
        const message = document.createElement("p");
        message.className = "friend-request-message";
        message.textContent = request.message;
        card.appendChild(message);
    }

    const actions = document.createElement("div");
    actions.className = "friend-request-actions";

    if (tab === "received") {
        const accept = document.createElement("button");
        accept.type = "button";
        accept.className = "friend-action";
        accept.textContent = "Chấp nhận";
        accept.addEventListener("click", () => acceptFriendRequest(itemId(request)));

        const decline = document.createElement("button");
        decline.type = "button";
        decline.className = "friend-action secondary";
        decline.textContent = "Từ chối";
        decline.addEventListener("click", () => declineFriendRequest(itemId(request)));

        actions.append(accept, decline);
    } else {
        const sent = document.createElement("small");
        sent.textContent = "Đang chờ phản hồi";
        actions.appendChild(sent);
    }

    card.appendChild(actions);
    return card;
}

// Chấp nhận lời mời kết bạn và cập nhật danh sách bạn bè.
async function acceptFriendRequest(requestId) {
    const data = await fetchData(API.acceptFriendRequest(requestId), { method: "POST" });

    state.friendRequests.received = state.friendRequests.received.filter(request => itemId(request) !== requestId);

    if (data.newFriend && !isFriend(itemId(data.newFriend))) {
        state.friends.unshift(data.newFriend);
    }

    renderFriendRequestBadge();
    renderFriendRequests();
    renderFriends();
}

// Từ chối lời mời kết bạn.
async function declineFriendRequest(requestId) {
    await apiFetch(API.declineFriendRequest(requestId), { method: "POST" });
    state.friendRequests.received = state.friendRequests.received.filter(request => itemId(request) !== requestId);
    renderFriendRequestBadge();
    renderFriendRequests();
}

// Đánh dấu conversation đang mở là đã xem.
async function markActiveConversationAsSeen() {
    if (!state.activeConversation) return;

    try {
        await fetchData(API.seen(itemId(state.activeConversation)), { method: "PATCH" });
    } catch {
        // Không chặn UI nếu trạng thái đã xem cập nhật thất bại.
    }
}

// Kết nối SignalR bằng access token hiện tại.
async function connectRealtime() {
    if (state.connection || !window.signalR) return;

    state.connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub", {
            accessTokenFactory: () => localStorage.getItem("accessToken") || ""
        })
        .withAutomaticReconnect()
        .build();

    state.connection.on("new-conversation", conversation => {
        upsertConversation(conversation);
    });

    state.connection.on("new-message", payload => {
        const message = payload?.message;
        const conversation = payload?.conversation;

        if (conversation) upsertConversation(conversation);
        if (message) {
            updateConversationPreviewFromMessage(message);
            appendRealtimeMessage(message);
        }
    });

    state.connection.on("read-message", payload => {
        if (payload?.conversation) upsertConversation(payload.conversation);
    });

    state.connection.on("friend-request-created", request => {
        if (request && !state.friendRequests.received.some(item => itemId(item) === itemId(request))) {
            state.friendRequests.received.unshift(request);
            renderFriendRequestBadge();
            renderFriendRequests();
        }
    });

    state.connection.on("friend-request-accepted", payload => {
        state.friendRequests.sent = state.friendRequests.sent.filter(request => itemId(request) !== payload?.requestId);

        if (payload?.user && !isFriend(itemId(payload.user))) {
            state.friends.unshift(payload.user);
        }

        renderFriendRequestBadge();
        renderFriendRequests();
        renderFriends();
    });

    state.connection.on("friend-request-declined", payload => {
        state.friendRequests.sent = state.friendRequests.sent.filter(request => itemId(request) !== payload?.requestId);
        renderFriendRequests();
    });

    await state.connection.start();
    state.conversations.forEach(conversation => joinConversation(itemId(conversation)));
}

// Join SignalR group của conversation để nhận realtime message.
function joinConversation(conversationId) {
    if (!conversationId || !state.connection || state.connection.state !== signalR.HubConnectionState.Connected) return;
    if (state.joinedConversationIds.has(conversationId)) return;

    state.joinedConversationIds.add(conversationId);
    state.connection.invoke("JoinConversation", conversationId).catch(() => {
        state.joinedConversationIds.delete(conversationId);
    });
}

// Tải dữ liệu thật từ backend, không dùng dữ liệu mẫu.
async function loadChatData() {
    setEmpty(groupList, "Đang tải nhóm chat...");
    setEmpty(friendList, "Đang tải bạn bè...");
    setEmpty(messageArea, "Chọn một cuộc trò chuyện để xem tin nhắn.");

    const [me, conversations, friends, friendRequests] = await Promise.allSettled([
        fetchData(API.me),
        fetchData(API.conversations),
        fetchData(API.friends),
        fetchData(API.friendRequests)
    ]);

    state.currentUser = me.status === "fulfilled" ? me.value : null;
    renderCurrentUser();

    state.conversations = conversations.status === "fulfilled" && Array.isArray(conversations.value)
        ? conversations.value
        : [];

    state.friends = friends.status === "fulfilled" && Array.isArray(friends.value)
        ? friends.value
        : [];

    if (friendRequests.status === "fulfilled") {
        state.friendRequests.received = Array.isArray(friendRequests.value.received) ? friendRequests.value.received : [];
        state.friendRequests.sent = Array.isArray(friendRequests.value.sent) ? friendRequests.value.sent : [];
    }

    renderGroups();
    renderFriends();
    renderFriendRequestBadge();
    renderFriendRequests();
    await connectRealtime();
}

messageArea.addEventListener("scroll", () => {
    if (messageArea.scrollTop <= 60) {
        loadOlderMessages();
    }
});

messageForm.addEventListener("submit", async event => {
    event.preventDefault();

    const content = messageInput.value.trim();
    if (!content || !state.activeConversation) return;

    messageInput.value = "";

    try {
        const activeConversationId = itemId(state.activeConversation);
        const message = await fetchData(API.sendMessage(activeConversationId), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ content })
        });

        const normalizedMessage = {
            ...message,
            conversationId: messageConversationId(message) || activeConversationId
        };

        updateConversationPreviewFromMessage(normalizedMessage);
        appendRealtimeMessage(normalizedMessage);
    } catch {
        messageInput.value = content;
    }
});

// Đóng khung tìm kiếm và xóa trạng thái tìm kiếm hiện tại.
function closeSearch() {
    searchBox.classList.remove("open");
    searchResult.replaceChildren();
    keywordInput.value = "";
    sidebar.classList.remove("search-active");
}

// Hiển thị thông báo trạng thái trong danh sách kết quả tìm kiếm.
function showSearchState(text) {
    sidebar.classList.add("search-active");
    setEmpty(searchResult, text);
}

// Tìm kiếm người dùng theo tên hoặc username từ API.
async function searchUsers() {
    const keyword = keywordInput.value.trim();
    if (!keyword) return showSearchState("Nhập tên hoặc username để tìm kiếm.");

    showSearchState("Đang tìm người dùng...");

    try {
        const users = await fetchData(API.search(keyword));
        searchResult.replaceChildren();
        users.forEach(user => searchResult.appendChild(createConversationCard(user, "search")));
        if (!users.length) showSearchState("Không tìm thấy người dùng.");
    } catch {
        showSearchState("Không tìm thấy người dùng.");
    }
}

// Đóng modal thông tin người dùng.
function closeUserProfile() {
    profileModal.classList.remove("open");
    profileModal.setAttribute("aria-hidden", "true");
}

// Tải và hiển thị thông tin chi tiết của người dùng được chọn.
function openSettingsModal() {
    const user = state.currentUser || {};
    settingsFirstName.value = userField(user, "firstName");
    settingsLastName.value = userField(user, "lastName");
    settingsNickname.value = userField(user, "nickname");
    settingsPhone.value = userField(user, "phone");
    settingsAvatarUrl.value = avatarUrlOf(user);
    settingsBio.value = userField(user, "bio");

    settingsModal.classList.add("open");
    settingsModal.setAttribute("aria-hidden", "false");
    settingsFirstName.focus();
}

function closeSettingsModal() {
    settingsModal.classList.remove("open");
    settingsModal.setAttribute("aria-hidden", "true");
}

async function submitSettings(event) {
    event.preventDefault();

    const submitButton = settingsForm.querySelector("button[type='submit']");
    submitButton.disabled = true;
    submitButton.textContent = "Đang lưu...";

    try {
        const updatedUser = await fetchData(API.updateMe, {
            method: "PATCH",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                firstName: settingsFirstName.value.trim() || null,
                lastName: settingsLastName.value.trim() || null,
                nickname: settingsNickname.value.trim() || null,
                phone: settingsPhone.value.trim() || null,
                avatarUrl: settingsAvatarUrl.value.trim() || null,
                bio: settingsBio.value.trim() || null
            })
        });

        state.currentUser = { ...state.currentUser, ...updatedUser };
        renderCurrentUser();
        closeSettingsModal();
    } catch (error) {
        alert(error.message || "Không thể cập nhật thông tin.");
    } finally {
        submitButton.disabled = false;
        submitButton.textContent = "Lưu thay đổi";
    }
}

async function openUserProfile(userId) {
    if (!userId) return;

    profileModal.classList.add("open");
    profileModal.setAttribute("aria-hidden", "false");
    profileModalContent.textContent = "Đang tải hồ sơ...";

    try {
        const user = await fetchData(API.profile(userId));
        const hero = document.createElement("div");
        hero.className = "profile-hero";
        hero.appendChild(createAvatar(user));

        const heroText = document.createElement("span");
        const heading = document.createElement("h2");
        heading.id = "profileModalName";
        heading.textContent = valueOr(user.displayname || user.displayName, "Người dùng");
        const usernameText = document.createElement("small");
        usernameText.textContent = `@${user.userName || user.username || "unknown"}`;
        heroText.append(heading, usernameText);
        hero.appendChild(heroText);

        const list = document.createElement("dl");
        [["Tên", user.displayname || user.displayName], ["Username", user.userName || user.username], ["Số điện thoại", user.phone], ["Nickname", user.nickname], ["Bio", user.bio]]
            .forEach(([label, value]) => {
                const row = document.createElement("div");
                const dt = document.createElement("dt");
                const dd = document.createElement("dd");
                dt.textContent = label;
                dd.textContent = valueOr(value);
                row.append(dt, dd);
                list.appendChild(row);
            });

        profileModalContent.replaceChildren(hero, list);
    } catch {
        profileModalContent.textContent = "Không thể tải thông tin người dùng.";
    }
}

// Chuyển theme sáng/tối và lưu lựa chọn vào localStorage.
const logoPalettes = [
    ["#9333ea", "#d946ef"],
    ["#f97316", "#fb7185"],
    ["#06b6d4", "#3b82f6"],
    ["#10b981", "#84cc16"],
    ["#ec4899", "#8b5cf6"],
    ["#f59e0b", "#ef4444"]
];

function applyLogoColor(accent, accent2) {
    document.documentElement.style.setProperty("--accent", accent);
    document.documentElement.style.setProperty("--accent-2", accent2);
    localStorage.setItem("bunny-logo-colors", JSON.stringify([accent, accent2]));
}

function randomLogoColor() {
    const [accent, accent2] = logoPalettes[Math.floor(Math.random() * logoPalettes.length)];
    applyLogoColor(accent, accent2);
}

function setTheme(theme) {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem("bunny-theme", theme);
    themeToggle.classList.toggle("on", theme === "dark");
}

el("openSearchBtn").addEventListener("click", () => {
    if (searchBox.classList.contains("open")) {
        closeSearch();
        return;
    }

    searchBox.classList.add("open");
    keywordInput.focus();
});

el("searchDismiss").addEventListener("click", closeSearch);
searchButton.addEventListener("click", searchUsers);
keywordInput.addEventListener("keydown", event => {
    if (event.key === "Enter") searchUsers();
});
themeToggle.addEventListener("click", () => setTheme(document.documentElement.dataset.theme === "dark" ? "light" : "dark"));
el("sidebarToggle").addEventListener("click", () => sidebar.classList.toggle("open"));
groupInfoButton.addEventListener("click", openConversationInfo);
el("openFriendRequestsBtn").addEventListener("click", openFriendModal);
el("openGroupCreateBtn").addEventListener("click", openGroupCreateModal);
el("openSettingsBtn").addEventListener("click", openSettingsModal);
friendRequestMessageForm.addEventListener("submit", submitFriendRequestMessage);
groupCreateForm.addEventListener("submit", submitGroupCreate);
groupNameInput.addEventListener("input", () => {
    groupNameError.textContent = "";
    groupNameInput.classList.remove("input-error");
});
groupMemberPicker.addEventListener("change", () => {
    groupMembersError.textContent = "";
});
settingsForm.addEventListener("submit", submitSettings);
randomLogoColorBtn.addEventListener("click", randomLogoColor);
document.querySelectorAll("[data-close-group-info]").forEach(button => button.addEventListener("click", closeGroupInfo));
document.querySelectorAll("[data-close-profile]").forEach(button => button.addEventListener("click", closeUserProfile));
document.querySelectorAll("[data-close-friend-modal]").forEach(button => button.addEventListener("click", closeFriendModal));
document.querySelectorAll("[data-close-friend-message]").forEach(button => button.addEventListener("click", closeFriendRequestMessageModal));
document.querySelectorAll("[data-close-group-create]").forEach(button => button.addEventListener("click", closeGroupCreateModal));
document.querySelectorAll("[data-close-settings]").forEach(button => button.addEventListener("click", closeSettingsModal));
document.querySelectorAll("[data-friend-tab]").forEach(button => {
    button.addEventListener("click", () => setFriendRequestTab(button.dataset.friendTab));
});
el("logoutBtn").addEventListener("click", async () => {
    try {
        await apiFetch("/api/auth/logout", { method: "POST" });
    } finally {
        localStorage.removeItem("accessToken");
        location.href = "/";
    }
});

window.BunnyChatUI = {
    state,
    renderGroups,
    renderFriends,
    renderMessages,
    renderGroupInfo,
    selectConversation,
    loadChatData
};

try {
    const savedLogoColors = JSON.parse(localStorage.getItem("bunny-logo-colors") || "null");
    if (Array.isArray(savedLogoColors) && savedLogoColors.length === 2) {
        applyLogoColor(savedLogoColors[0], savedLogoColors[1]);
    }
} catch {
    localStorage.removeItem("bunny-logo-colors");
}

setTheme(document.documentElement.dataset.theme || "dark");
loadChatData();
