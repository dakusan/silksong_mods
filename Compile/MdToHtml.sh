TITLE="$1"

#Pull from STDIN
cat |

#Change ___markdown-content___ to body and " ---" to <hr class=small>
perl -pe 's/(body )?#___markdown-content___/body/g; s/^ ?---/<hr class=small>\n/g' |

#Convert .md to .html
pandoc  -f gfm+raw_html+strikeout -t html --standalone --quiet |

#Add the html title and the stylesheet in the head
perl -pe 's{<title\b[^>]*>-</title>}{<title>'"$TITLE"'</title>}i; s{<head\b[^>]*>}{<head>\n<style>\nbody ul, body p { margin-top:0; }\nhtml body ul { margin-bottom:15px; }\nhtml body ul ul { margin-bottom:0; }\nbody hr { margin-top:0; background-color:#d8dee4; }\nhr.small { height:1px; }\nbody br.Hide { display:initial; }\n</style>}i' |

#Remove paragraphs on lines that have <center>
perl -0777 -pe 's{<p\b[^>]*>\s*((?:(?!</p\b).)*?<center\b.*?)(?:</p\s*>)}{$1}gis' |

#Send to STDOUT
cat