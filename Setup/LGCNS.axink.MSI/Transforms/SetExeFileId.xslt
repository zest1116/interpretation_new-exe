<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:wix="http://wixtoolset.org/schemas/v4/wxs"
    exclude-result-prefixes="wix">

	<xsl:output method="xml" indent="yes"/>

	<!-- 기본: 모든 노드 그대로 복사 -->
	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- LGCNS.DesktopAudioHub.App.exe 파일에 고정 ID 부여 -->
	<xsl:template match="wix:File[substring(@Source, string-length(@Source) - string-length('axink.exe') + 1) = 'axink.exe']/@Id">
		<xsl:attribute name="Id">MainExeFile</xsl:attribute>
	</xsl:template>

</xsl:stylesheet>