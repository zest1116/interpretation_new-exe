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

	<!-- axink Translator.exe 파일에 고정 ID 부여 -->
	<xsl:template match="wix:File[substring(@Source, string-length(@Source) - string-length('axink Translator.exe') + 1) = 'axink Translator.exe']/@Id">
		<xsl:attribute name="Id">MainExeFile</xsl:attribute>
	</xsl:template>

	<!-- 수동 컴포넌트와 중복되는 Updater 파일을 Heat 수집에서 제외 -->
	<xsl:template match="wix:Component[contains(wix:File/@Source, 'Updater.exe')]" />
	<xsl:template match="wix:Component[contains(wix:File/@Source, 'Updater.dll')]" />
	<xsl:template match="wix:Component[contains(wix:File/@Source, 'Updater.deps.json')]" />
	<xsl:template match="wix:Component[contains(wix:File/@Source, 'Updater.runtimeconfig.json')]" />

</xsl:stylesheet>