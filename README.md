BlogMLToMarkdown
================

A C# command line tool for transforming a Blog ML document to markdown documents 
for Jekyll or Pretzel. It uses pandoc for transforming HTML into Markdown. 

It's a customized version of [BlogMLToMarkdown](https://github.com/pcibraro/BlogMLToMarkdown),
but with changes very specific to my requirements:
For each blog post in BlogML.xml, it tries to download images, attachments etc from the 
original site and updates the links.

Comments are written to ```disqus.wxr``` to be imported in Disqus.
  
