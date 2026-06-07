"# BunnyChat" 
 API design:
    https://docs.google.com/spreadsheets/d/1vK-KL4gsVap8F7eGRWMvcSkOwDS9jRyC4F_1PbNZL4E/edit?gid=0#gid=0
    
Graph:
    http://127.0.0.1:5173/?token=a6c2d903b709031918ffd112f767dfe1
    
Pass Mông: mongodb+srv://ttthinh2005_db_user:abc123456789@cluster0.58iwirh.mongodb.net/chatapp?appName=Cluster0

framework tự kiểm tra token do ai phát hành, kiểm tra token hết hạn hay chưa, Token dành cho đúng app nhận, Token do đúng ai phát hành


    Vì middleware [Authorize] chỉ kiểm tra JWT, không kiểm tra session trong MongoDB của bạn.  AccessToken có đúng chữ ký không?
    AccessToken còn hạn không?
    Issuer/Audience đúng không?

    Nhưng nó không biết:

    Session trong MongoDB còn hay đã bị xóa?
    User đã logout chưa?
    RefreshToken còn hợp lệ không?

dotnet restore BunnyChat.csproj 

dotnet clean BunnyChat.csproj
dotnet build BunnyChat.csproj
dotnet watch run --project BunnyChat.csproj



 cd C:\Users\Admin\Documents\BunnyChat\BunnyChat
 
taskkill /F /IM dotnet.exe

Remove-Item -Recurse -Force .\bin
Remove-Item -Recurse -Force .\obj

dotnet watch run --project .\BunnyChat.csproj