"# DoAnWeb" 
 API design:
    https://docs.google.com/spreadsheets/d/1vK-KL4gsVap8F7eGRWMvcSkOwDS9jRyC4F_1PbNZL4E/edit?gid=0#gid=0

Pass Mông: mongodb+srv://ttthinh2005_db_user:RlZT2gdW4kz0srHa%40@cluster0.58iwirh.mongodb.net/chatapp?appName=Cluster0

framework tự kiểm tra token do ai phát hành, kiểm tra token hết hạn hay chưa, Token dành cho đúng app nhận, Token do đúng ai phát hành

dotnet clean WebMoi.csproj
dotnet build WebMoi.csproj
dotnet watch run --project WebMoi.csproj



 cd C:\Users\Admin\Documents\doanWeb\webmoi
 
taskkill /F /IM dotnet.exe

Remove-Item -Recurse -Force .\bin
Remove-Item -Recurse -Force .\obj

dotnet watch run --project .\WebMoi.csproj