import urllib.request
import json

req = urllib.request.Request('https://api.github.com/repos/SoftBlack42/StarPie/issues/100', headers={'User-Agent': 'Mozilla/5.0'})
res = json.loads(urllib.request.urlopen(req).read().decode('utf-8'))
with open('g:/Users/2 Better/Desktop/design/issue100.txt', 'w', encoding='utf-8') as f:
    f.write('Title: ' + str(res.get('title')) + '\n\n')
    f.write('Body:\n' + str(res.get('body')) + '\n\n')
    
    comments_url = res.get('comments_url')
    if comments_url:
        creq = urllib.request.Request(comments_url, headers={'User-Agent': 'Mozilla/5.0'})
        cres = json.loads(urllib.request.urlopen(creq).read().decode('utf-8'))
        for c in cres:
            f.write(f"--- Comment by {c['user']['login']} ---\n")
            f.write(str(c.get('body')) + '\n\n')

print("Issue 100 written successfully")
