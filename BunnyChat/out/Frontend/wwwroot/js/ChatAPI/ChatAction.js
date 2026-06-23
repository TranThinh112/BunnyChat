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


const API = {
    groups: "/api/chats/groups",
    friends: "/api/friends",
    messages: id => `/api/chats/${encodeURIComponent(id)}/messages`,
    sendMessage: id => `/api/chats/${encodeURIComponent(id)}/messages`,
    ...(window.BunnyChatAPI || {})
};

// Dữ liệu demo dùng khi server chưa trả về danh sách chat.
const DEMO = {
    groups: [{
        id: "demo-backend",
        name: "Nhóm Backend Dev",
        memberCount: 3,
        createdAt: "2026-05-20T08:30:00",
        members: [
            { displayname: "Thịnh Trần", username: "trthinh1112" },
            { displayname: "M tikcode", username: "mtikcode" },
            { displayname: "Mai Lê", username: "maile" }
        ],
        lastMessage: "ok",
        demo: true,
        messages: [
            { content: "quá đẹp", createdAt: "2026-06-11T15:45:00", isMine: false },
            { content: "mai demo xong đi ăn lẩu", createdAt: "2026-06-11T15:45:00", isMine: false },
            { content: "vậy tối nay đi ăn bún đậu không", createdAt: "2026-06-11T15:46:00", isMine: false },
            { content: "nhầm kênh", createdAt: "2026-06-11T15:46:00", isMine: false },
            { content: "ok", createdAt: "2026-06-11T15:53:00", isMine: false },
            { content: "thôi ăn cái khác đi", createdAt: "2026-06-12T10:05:00", isMine: true },
            { content: "mà ăn gì cũng được", createdAt: "2026-06-12T10:05:00", isMine: true }
        ]
    }],
    friends: [{
        id: "demo-friend",
        displayname: "M tikcode",
        username: "mtikcode",
        online: true,
        lastMessage: "ok",
        demo: true,
        messages: [
            { content: "Chào bạn, BunnyChat đẹp quá!", createdAt: "2026-06-12T09:30:00", isMine: false },
            { content: "Cảm ơn bạn nhé", createdAt: "2026-06-12T09:31:00", isMine: true }
        ]
    },
    {
        id: "demo-friend",
        displayname: "chim to vai lon",
        username: "toi an cut",
        online: true,
        lastMessage: "ok",
        demo: true,
        messages: [
            { content: "Chào bạn, BunnyChat đẹp quá!", createdAt: "2026-06-12T09:30:00", isMine: false },
            { content: "Cảm ơn bạn nhé", createdAt: "2026-06-12T09:31:00", isMine: true }
        ]
    }]
};

let activeConversation = null;

// Trả về giá trị hợp lệ hoặc nội dung mặc định khi dữ liệu bị trống.
function valueOr(value, fallback = "Chưa cập nhật") {
    return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

// Lấy chữ cái đầu để hiển thị trong avatar.
function initialOf(value) {
    return valueOr(value, "?").charAt(0).toUpperCase();
}

// Hiển thị trạng thái trống hoặc trạng thái đang tải trong container.
function setEmpty(container, text) {
    const empty = document.createElement("p");
    empty.className = "empty-state";
    empty.textContent = text;
    container.replaceChildren(empty);
}

// Tạo phần tử avatar từ tên người dùng hoặc tên nhóm.
function createAvatar(name) {
    const avatar = document.createElement("span");
    avatar.className = "avatar";
    avatar.textContent = initialOf(name);
    return avatar;
}

// Chuẩn hóa tên hiển thị của nhóm chat, bạn bè hoặc người dùng.
function conversationName(item) {
    return valueOr(item.name || item.displayname || item.displayName || item.username, "Cuộc trò chuyện");
}

// Tạo card cuộc trò chuyện và gắn hành động phù hợp theo loại card.
function createConversationCard(item, type) {
    const name = conversationName(item);
    const card = document.createElement("button");
    card.type = "button";
    card.className = "conversation-card";

    const info = document.createElement("span");
    info.className = "conversation-info";
    const title = document.createElement("strong");
    title.textContent = name;
    const preview = document.createElement("small");
    preview.textContent = item.lastMessage || item.username || (type === "group" ? `${item.memberCount || 0} thành viên` : "Chưa có tin nhắn");
    info.append(title, preview);
    card.append(createAvatar(name), info);

    if (type === "search") {
        card.addEventListener("click", () => openUserProfile(item.id));
    } else if (type !== "member") {
        card.addEventListener("click", () => selectConversation(item, type, card));
    }
    return card;
}

// Render danh sách card vào container và cập nhật tổng số phần tử.
function renderList(container, count, items, type) {
    container.replaceChildren();
    count.textContent = String(items.length);
    items.forEach(item => container.appendChild(createConversationCard(item, type)));
}

// Render danh sách nhóm chat từ dữ liệu server hoặc dữ liệu demo.
function renderGroups(groups = []) {
    renderList(groupList, el("groupCount"), groups, "group");
}

// Render danh sách bạn bè từ dữ liệu server hoặc dữ liệu demo.
function renderFriends(friends = []) {
    renderList(friendList, el("friendCount"), friends, "friend");
}

// Tạo phần tử tin nhắn gửi hoặc nhận từ object tin nhắn.
function createMessage(message) {
    const row = document.createElement("article");
    row.className = `message ${message.isMine ? "outgoing" : "incoming"}`;
    const bubble = document.createElement("p");
    bubble.textContent = message.content || message.text || "";
    const time = document.createElement("time");
    time.textContent = message.createdAt
        ? new Date(message.createdAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })
        : "";
    row.append(bubble, time);
    return row;
}

// Render toàn bộ tin nhắn của cuộc trò chuyện đang được chọn.
function renderMessages(messages = []) {
    messageArea.replaceChildren();
    messages.forEach(message => messageArea.appendChild(createMessage(message)));
    if (!messages.length) setEmpty(messageArea, "Chưa có tin nhắn.");
    messageArea.scrollTop = messageArea.scrollHeight;
}

// Gọi API có access token và trả về phần dữ liệu chính của response.
async function fetchData(url, options) {
    const response = await apiFetch(url, options);
    if (!response.ok) throw new Error("Request failed");
    const result = await response.json();
    return result.data ?? result;
}

// Chọn cuộc trò chuyện, cập nhật header và tải danh sách tin nhắn.
async function selectConversation(item, type, card) {
    activeConversation = { ...item, id: item.id || item._id, type };
    document.querySelector(".conversation-card.active")?.classList.remove("active");
    card.classList.add("active");
    const name = conversationName(item);
    el("chatTitle").textContent = name;
    el("chatStatus").textContent = type === "group" ? `${item.memberCount || 0} thành viên` : (item.online ? "Đang hoạt động" : "Ngoại tuyến");
    el("chatAvatar").textContent = initialOf(name);
    groupInfoButton.disabled = type !== "group";
    renderGroupInfo(type === "group" ? item : null);
    messageInput.disabled = false;
    sendButton.disabled = false;
    messageInput.placeholder = "Soạn tin nhắn...";

    if (item.demo) {
        renderMessages(item.messages);
        return;
    }

    setEmpty(messageArea, "Đang tải tin nhắn...");
    try {
        renderMessages(await fetchData(API.messages(activeConversation.id)));
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
    [["Tên nhóm", name], ["Số thành viên", String(group.memberCount || group.members?.length || 0)], ["Ngày tạo", createdAt]].forEach(([label, value]) => {
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

    groupInfoContent.append(hero, details, members);
}

// Mở drawer thông tin của nhóm đang được chọn.
function openGroupInfo() {
    if (!activeConversation || activeConversation.type !== "group") return;
    groupInfo.classList.add("open");
    groupInfo.setAttribute("aria-hidden", "false");
}

// Đóng drawer thông tin nhóm.
function closeGroupInfo() {
    groupInfo.classList.remove("open");
    groupInfo.setAttribute("aria-hidden", "true");
}

// Hiển thị dữ liệu demo trước, sau đó thay thế bằng dữ liệu server nếu có.
async function loadChatData() {
    renderGroups(DEMO.groups);
    renderFriends(DEMO.friends);
    setEmpty(messageArea, "Chọn một cuộc trò chuyện để xem tin nhắn.");

    const [groups, friends] = await Promise.allSettled([fetchData(API.groups), fetchData(API.friends)]);
    if (groups.status === "fulfilled" && Array.isArray(groups.value) && groups.value.length) renderGroups(groups.value);
    if (friends.status === "fulfilled" && Array.isArray(friends.value) && friends.value.length) renderFriends(friends.value);
}

messageForm.addEventListener("submit", async event => {
    event.preventDefault();
    const content = messageInput.value.trim();
    if (!content || !activeConversation) return;
    messageInput.value = "";

    if (activeConversation.demo) {
        messageArea.querySelector(".empty-state")?.remove();
        messageArea.appendChild(createMessage({ content, createdAt: new Date().toISOString(), isMine: true }));
        messageArea.scrollTop = messageArea.scrollHeight;
        return;
    }

    try {
        const message = await fetchData(API.sendMessage(activeConversation.id), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ content })
        });
        messageArea.appendChild(createMessage({ ...message, content, isMine: true }));
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
        const users = await fetchData(`/api/users/search?q=${encodeURIComponent(keyword)}`);
        searchResult.replaceChildren();
        users.forEach(user => searchResult.appendChild(createConversationCard(user, "search")));
        if (!users.length) showSearchState("Không tìm thấy người dùng.");
    } catch {
        showSearchState("Không thể tìm kiếm lúc này.");
    }
}

// Đóng modal thông tin người dùng.
function closeUserProfile() {
    profileModal.classList.remove("open");
    profileModal.setAttribute("aria-hidden", "true");
}

// Tải và hiển thị thông tin chi tiết của người dùng được chọn.
async function openUserProfile(userId) {
    if (!userId) return;
    profileModal.classList.add("open");
    profileModal.setAttribute("aria-hidden", "false");
    profileModalContent.textContent = "Đang tải hồ sơ...";
    try {
        const user = await fetchData(`/api/users/${encodeURIComponent(userId)}/profile`);
        const heading = document.createElement("h2");
        heading.id = "profileModalName";
        heading.textContent = valueOr(user.displayname, "Người dùng");
        const list = document.createElement("dl");
        [["Tên", user.displayname], ["Username", user.username], ["Số điện thoại", user.phone], ["Nickname", user.nickname], ["Bio", user.bio]].forEach(([label, value]) => {
            const row = document.createElement("div");
            const dt = document.createElement("dt");
            const dd = document.createElement("dd");
            dt.textContent = label;
            dd.textContent = valueOr(value);
            row.append(dt, dd);
            list.appendChild(row);
        });
        profileModalContent.replaceChildren(heading, list);
    } catch {
        profileModalContent.textContent = "Không thể tải thông tin người dùng.";
    }
}

// Chuyển theme sáng/tối và lưu lựa chọn vào localStorage.
function setTheme(theme) {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem("bunny-theme", theme);
    themeToggle.classList.toggle("on", theme === "dark");
}

el("openSearchBtn").addEventListener("click", () => searchBox.classList.contains("open") ? closeSearch() : (searchBox.classList.add("open"), keywordInput.focus()));
el("searchDismiss").addEventListener("click", closeSearch);
searchButton.addEventListener("click", searchUsers);
keywordInput.addEventListener("keydown", event => { if (event.key === "Enter") searchUsers(); });
themeToggle.addEventListener("click", () => setTheme(document.documentElement.dataset.theme === "dark" ? "light" : "dark"));
el("sidebarToggle").addEventListener("click", () => sidebar.classList.toggle("open"));
groupInfoButton.addEventListener("click", openGroupInfo);
document.querySelectorAll("[data-close-group-info]").forEach(button => button.addEventListener("click", closeGroupInfo));
document.querySelectorAll("[data-close-profile]").forEach(button => button.addEventListener("click", closeUserProfile));
el("logoutBtn").addEventListener("click", async () => {
    try {
        await apiFetch("/api/auth/logout", {
            method: "POST"
        });;
    } finally {
        localStorage.removeItem("accessToken");
        location.href = "/";
    }
});

window.BunnyChatUI = { renderGroups, renderFriends, renderMessages, renderGroupInfo, selectConversation, loadChatData, demo: DEMO };
setTheme(document.documentElement.dataset.theme || "dark");
loadChatData();
