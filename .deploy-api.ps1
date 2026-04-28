cd C:\repos\trumpStockAlert\backend

Remove-Item .\publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\app.zip -Force -ErrorAction SilentlyContinue

dotnet publish -c Release -r linux-x64 --self-contained false -o .\publish
tar -a -c -f .\app.zip -C .\publish .

az webapp deploy `
  --resource-group trumpstockalertrg `
  --name trump-stock-alert-api `
  --src-path .\app.zip `
  --type zip