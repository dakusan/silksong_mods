#!/bin/bash
#Convert [Visual Studio] markdown to HTML

#The first parameter will be inserted into the HTML as a title
TITLE="$1"

#Pull from STDIN
cat |

#Change ___markdown-content___ to body and " ---" to <hr class=small>
perl -pe 's/(body )?#___markdown-content___/body/g; s/^ ?---/<hr class=small>\n/g' |

#Convert .md to .html
pandoc -f gfm+raw_html+strikeout -t html --quiet |

#Add the html title and the stylesheet in the head
{
	echo -e '<!DOCTYPE html>\n<html xmlns="http://www.w3.org/1999/xhtml">\n<head>\n\t<meta charset="UTF-8">\n\t<title>'"$TITLE"'</title>'
	echo '<style>
body ul, body p { margin-top:0; }
html body ul, html body ol { margin-bottom:15px; padding-left:1.7em; }
html body ul ul, html body ul ol, html body ol ul, html body ol ol { margin-bottom:0; }
body hr { margin-top:0; background-color:#d8dee4; border-color:#d8dee4; }
hr.small { height:1px; border:0; margin:0 0 1em 0; }
body br.Hide { display:initial; }
</style>
</head><body>'
	cat;
	echo -e '</body>\n</html>'
} |

#Remove paragraphs on lines that have <center> or <right>
perl -0777 -pe 's{<p\b[^>]*>\s*((?:(?!</p\b).)*?<(?:center|right)\b.*?)(?:</p\s*>)}{$1}gis' |

#Send to STDOUT
cat